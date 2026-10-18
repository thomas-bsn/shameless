// Le serveur du wizard : accès par code, validation, écriture de la configuration, suivi du démarrage.
// Le wizard ne journalise que des événements (jamais une saisie, un secret ou un corps de requête).
import { randomBytes, timingSafeEqual } from "node:crypto";
import { existsSync, readFileSync, renameSync, statSync, writeFileSync } from "node:fs";
import type { IncomingMessage, RequestListener, ServerResponse } from "node:http";
import { extname, join, normalize } from "node:path";
import { buildInstanceEnv, serializeEnv, validateConfig, type Config, type Generated } from "./config.ts";
import { genererSecretsUchuu } from "./secrets.ts";
import { construireRecapUchuu, type RecapUchuu } from "./uchuu.ts";

export type DiscordCheck = "ok" | "refused" | "unreachable";
/** Résultat de l'échange du code OAuth Discord (scope identify) contre l'identité de l'Opérateur. */
export type DiscordLogin = { ok: true; id: string; username: string } | { ok: false; reason: "refused" | "unreachable" };
export type Phase = "locked" | "configuring" | "installing" | "done" | "failed";

export interface WizardOptions {
  /** Volume partagé : instance.env puis le marqueur ready y sont écrits. */
  instanceDir: string;
  /** Dossier du front statique du wizard (facultatif : absent dans les tests). */
  publicDir?: string;
  log: (message: string) => void;
  now: () => number;
  codeTtlMs: number;
  /** Nombre d'essais de code ratés avant d'en générer un nouveau. */
  maxAttempts: number;
  checkDiscord: (clientId: string, clientSecret: string) => Promise<DiscordCheck>;
  /** Échange le code du retour OAuth contre l'identifiant Discord de l'Opérateur (jamais journalisé). */
  loginDiscord: (demande: { code: string; clientId: string; clientSecret: string; redirectUri: string }) => Promise<DiscordLogin>;
  /** Vrai quand l'API répond « en bonne santé » (via le réseau Compose). */
  probeApi: () => Promise<boolean>;
  pollIntervalMs: number;
  installTimeoutMs: number;
  /** Délai laissé au navigateur pour lire l'état final avant l'arrêt du wizard. */
  finishGraceMs: number;
  onFinished: () => void;
}

const API = "/_setup/api/";
const ALPHABET = "ABCDEFGHJKMNPQRSTVWXYZ23456789";
const ALNUM = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
const MAX_BODY = 16 * 1024;
const STATE_TTL_MS = 10 * 60_000;
const CALLBACK = "discord/callback";

const MIME: Record<string, string> = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".png": "image/png",
  ".svg": "image/svg+xml",
  ".json": "application/json",
};

/** Une Instance est configurée dès que le marqueur ready existe : le wizard ne doit alors plus jamais s'ouvrir. */
export function isConfigured(instanceDir: string): boolean {
  return existsSync(join(instanceDir, "ready"));
}

function tirer(alphabet: string, longueur: number): string {
  // 256 n'est pas multiple de la taille de l'alphabet : on écarte les octets qui biaiseraient le tirage.
  const limite = 256 - (256 % alphabet.length);
  let r = "";
  while (r.length < longueur)
    for (const b of randomBytes(longueur * 2)) if (b < limite && r.length < longueur) r += alphabet[b % alphabet.length];
  return r;
}

const nouveauCode = () => tirer(ALPHABET, 16).replace(/(.{4})(?=.)/g, "$1-");
const normaliser = (c: string) => c.toUpperCase().replace(/[^A-Z0-9]/g, "");

function egal(a: string, b: string): boolean {
  const x = Buffer.from(a);
  const y = Buffer.from(b);
  return x.length === y.length && timingSafeEqual(x, y);
}

function ecrireAtomique(chemin: string, contenu: string) {
  const tmp = `${chemin}.tmp`;
  writeFileSync(tmp, contenu, { mode: 0o644 });
  renameSync(tmp, chemin);
}

export function createWizard(o: WizardOptions) {
  let code = "";
  let codeEmisA = 0;
  let codeUtilise = false;
  let essais = 0;
  let session: { token: string; expire: number } | null = null;
  let phase: Phase = "locked";
  let publicUrl = "";
  let failure = "";
  let warnings: string[] = [];
  let generated: Generated | null = null;
  // Récapitulatif Uchuu (contient les secrets) : en mémoire seulement, jamais journalisé.
  let recapUchuu: RecapUchuu | null = null;
  let termine = false;
  // Connexion Discord de l'Opérateur : une seule tentative en cours, en mémoire seulement.
  let pending: { state: string; clientId: string; clientSecret: string; redirectUri: string; at: number } | null = null;
  let superadmin: { id: string; username: string } | null = null;
  let discordError = "";

  const emettreCode = (raison: string) => {
    code = nouveauCode();
    codeEmisA = o.now();
    codeUtilise = false;
    essais = 0;
    // Le code est le seul secret volontairement écrit dans les logs : c'est ainsi que l'Opérateur le lit.
    o.log(`${raison}\nCode d'accès : ${code}\n(à usage unique, valable ${Math.round(o.codeTtlMs / 60_000)} minutes)`);
  };
  emettreCode("Wizard d'installation prêt.");

  const sessionValide = (req: IncomingMessage): boolean => {
    if (!session || o.now() > session.expire) return false;
    const cookie = /(?:^|;\s*)shameless_wizard=([A-Za-z0-9]+)/.exec(req.headers.cookie ?? "")?.[1];
    return !!cookie && egal(cookie, session.token);
  };

  const etat = (req: IncomingMessage) => {
    if (!sessionValide(req)) return { phase: "locked" satisfies Phase, codeExpired: !codeUtilise && o.now() > codeEmisA + o.codeTtlMs };
    return {
      phase,
      ...(publicUrl ? { publicUrl } : {}),
      ...(phase === "failed" ? { failure } : {}),
      ...(warnings.length > 0 ? { warnings } : {}),
      ...(recapUchuu ? { uchuu: recapUchuu } : {}),
      ...(superadmin ? { discord: superadmin } : {}),
      ...(discordError ? { discordError } : {}),
    };
  };

  const surveillerDemarrage = () => {
    const debut = o.now();
    let reel = Date.now();
    const limite = reel + o.installTimeoutMs;
    const tour = async () => {
      if (await o.probeApi().catch(() => false)) {
        phase = "done";
        session && (session.expire = Number.MAX_SAFE_INTEGER);
        o.log("Installation terminée : l'API répond. Le wizard s'arrête.");
        setTimeout(() => {
          termine = true;
          session = null;
          pending = null;
          o.onFinished();
        }, o.finishGraceMs);
        return;
      }
      reel = Date.now();
      if (reel > limite) {
        phase = "failed";
        failure =
          "L'API n'a pas démarré dans le délai prévu. Vérifiez la configuration saisie, puis consultez les journaux avec « docker compose logs migrate api » pour connaître la cause exacte. Vous pouvez corriger et réessayer.";
        o.log(`Le démarrage de l'API n'a pas abouti après ${Math.round((o.now() - debut) / 1000)} s : échec signalé à l'Opérateur.`);
        return;
      }
      setTimeout(tour, o.pollIntervalMs);
    };
    setTimeout(tour, o.pollIntervalMs);
  };

  async function lireJson(req: IncomingMessage): Promise<Record<string, unknown> | null> {
    let taille = 0;
    const morceaux: Buffer[] = [];
    for await (const m of req) {
      taille += (m as Buffer).length;
      if (taille > MAX_BODY) return null;
      morceaux.push(m as Buffer);
    }
    try {
      const v = JSON.parse(Buffer.concat(morceaux).toString("utf8") || "{}");
      return v && typeof v === "object" && !Array.isArray(v) ? v : null;
    } catch {
      return null;
    }
  }

  const repondre = (res: ServerResponse, status: number, corps?: unknown, extra: Record<string, string> = {}) => {
    res.writeHead(status, {
      "content-type": "application/json; charset=utf-8",
      "cache-control": "no-store",
      "x-content-type-options": "nosniff",
      ...extra,
    });
    res.end(corps === undefined ? undefined : JSON.stringify(corps));
  };

  async function api(req: IncomingMessage, res: ServerResponse, route: string) {
    if (req.method === "GET" && route === "state") return repondre(res, 200, etat(req));
    if (req.method === "GET" && route === CALLBACK) return retourDiscord(req, res);

    if (req.method !== "POST") return repondre(res, 405, { message: "Méthode non autorisée." });
    if (!/^application\/json/.test(req.headers["content-type"] ?? ""))
      return repondre(res, 415, { message: "Le contenu doit être du JSON." });
    const corps = await lireJson(req);
    if (!corps) return repondre(res, 400, { message: "Requête illisible." });

    if (route === "unlock") {
      if (termine) return repondre(res, 401, { message: "L'installation est terminée." });
      if (codeUtilise) return repondre(res, 401, { message: "Ce code a déjà servi. Relancez le wizard (docker compose restart wizard) pour en obtenir un nouveau." });
      if (o.now() > codeEmisA + o.codeTtlMs)
        return repondre(res, 410, { message: "Le code a expiré. Relancez le wizard (docker compose restart wizard) pour en obtenir un nouveau dans les logs." });
      const saisi = normaliser(typeof corps.code === "string" ? corps.code : "");
      if (!egal(saisi, normaliser(code))) {
        o.log("Tentative de déverrouillage refusée.");
        if (++essais >= o.maxAttempts) emettreCode("Trop d'essais : un nouveau code a été généré.");
        return repondre(res, 401, { message: "Code incorrect. Reprenez-le tel qu'affiché dans « docker compose logs wizard »." });
      }
      codeUtilise = true;
      session = { token: tirer(ALNUM, 40), expire: o.now() + o.codeTtlMs };
      phase = "configuring";
      o.log("Session d'installation ouverte : le code est maintenant invalide.");
      const secure = req.headers["x-forwarded-proto"] === "https" ? "; Secure" : "";
      return repondre(res, 200, { phase }, {
        "set-cookie": `shameless_wizard=${session.token}; HttpOnly; SameSite=Strict; Path=/_setup; Max-Age=${Math.round(o.codeTtlMs / 1000)}${secure}`,
      });
    }

    if (!sessionValide(req)) return repondre(res, 401, { message: "Accès verrouillé : saisissez le code affiché dans les logs." });

    if (route === "validate") {
      const { errors, warnings } = validateConfig(corps);
      return repondre(res, 200, { errors, warnings, callbackUrl: callback(corps), wizardRedirectUri: redirectDuWizard(corps) });
    }

    if (route === "discord/start") {
      const { errors } = validateConfig(corps);
      const redirectUri = redirectDuWizard(corps);
      const bloquantes = Object.fromEntries(Object.entries(errors).filter(([k]) => ["publicUrl", "clientId", "clientSecret"].includes(k)));
      if (Object.keys(bloquantes).length > 0 || !redirectUri) return repondre(res, 422, { errors: bloquantes });
      const clientId = String(corps.clientId).trim();
      const clientSecret = String(corps.clientSecret).trim();
      pending = { state: tirer(ALNUM, 40), clientId, clientSecret, redirectUri, at: o.now() };
      discordError = "";
      const authorize = new URL("https://discord.com/oauth2/authorize");
      authorize.search = new URLSearchParams({ client_id: clientId, response_type: "code", scope: "identify", redirect_uri: redirectUri, state: pending.state, prompt: "consent" }).toString();
      return repondre(res, 200, { authorizeUrl: authorize.toString(), redirectUri });
    }

    if (route === "install") {
      if (phase !== "configuring" && phase !== "failed") return repondre(res, 409, { message: "Une installation est déjà en cours." });
      const verdict = validateConfig(corps);
      const { errors, value } = verdict;
      if (!value) return repondre(res, 422, { errors });
      if (!superadmin)
        return repondre(res, 422, { errors: { discord: "Connectez-vous avec Discord à l'étape Discord : votre compte devient le premier Superadministrateur de l'Instance." } });
      value.superadminId = superadmin.id;

      warnings = [...verdict.warnings];
      const discord = await o.checkDiscord(value.clientId, value.clientSecret);
      if (discord === "refused")
        return repondre(res, 422, {
          errors: { clientSecret: "Discord refuse ce Client ID / Client Secret. Vérifiez-les dans le portail développeur (page OAuth2) ; au besoin, « Reset Secret »." },
        });
      if (discord === "unreachable")
        warnings.push("Discord n'a pas pu être joint pour vérifier ces identifiants : ils seront testés au premier démarrage.");

      // Page servie en HTTPS par un proxy en amont qui joint Caddy en HTTP (en-têtes posés par Caddy) :
      // Caddy ne doit pas chercher à gérer le HTTPS, sinon il redirigerait en boucle vers lui-même.
      // X-Forwarded-Proto n'est conservé par Caddy que d'un proxy du réseau privé : si le proxy en amont
      // n'en fait pas partie, l'en-tête est perdu. L'Origin posée par le navigateur sur ce POST dit aussi
      // que la page est vue en HTTPS alors qu'elle est entrée en HTTP.
      const pageEnHttps = req.headers["x-forwarded-proto"] === "https" || String(req.headers.origin ?? "").startsWith("https://");
      value.tlsEnAmont = req.headers["x-shameless-entry"] === "http" && pageEnHttps;
      if (value.tlsEnAmont && value.publicUrl.startsWith("https://"))
        warnings.push("Le HTTPS est géré par le proxy en amont de Shameless : Caddy sert l'Instance en HTTP derrière lui.");
      installer(value);
      return repondre(res, 202, { phase, warnings, ...(recapUchuu ? { uchuu: recapUchuu } : {}) });
    }

    return repondre(res, 404, { message: "Inconnu." });
  }

  const pageDiscord = (res: ServerResponse, status: number, titre: string, detail: string) => {
    const echapper = (t: string) => t.replace(/[&<>"']/g, (c) => `&#${c.charCodeAt(0)};`);
    res.writeHead(status, {
      "content-type": "text/html; charset=utf-8",
      "cache-control": "no-store",
      "x-content-type-options": "nosniff",
      "x-frame-options": "DENY",
      "referrer-policy": "no-referrer",
    });
    res.end(
      `<!doctype html><html lang="fr"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Installation de Shameless</title>` +
        `<body style="font-family:system-ui,sans-serif;max-width:32rem;margin:3rem auto;padding:0 1rem"><h1>${echapper(titre)}</h1><p>${echapper(detail)}</p>` +
        `<p>Vous pouvez fermer cette fenêtre et revenir à l'installation.</p></body></html>`,
    );
  };

  // Retour de Discord : une navigation venue d'un autre site, donc sans cookie de session (SameSite=Strict).
  // Le state à usage unique, lié à la tentative démarrée par l'Opérateur, est la seule preuve.
  async function retourDiscord(req: IncomingMessage, res: ServerResponse) {
    const params = new URL(req.url ?? "", "http://wizard").searchParams;
    const attendu = pending;
    const state = params.get("state") ?? "";
    if (!attendu || termine || !egal(state, attendu.state))
      return pageDiscord(res, 400, "Connexion Discord impossible", "Cette connexion n'est pas reconnue : elle a été remplacée ou a déjà servi. Relancez-la depuis le wizard.");
    pending = null;
    if (o.now() > attendu.at + STATE_TTL_MS)
      return pageDiscord(res, 400, "Connexion Discord expirée", "La connexion a expiré (10 minutes). Relancez-la depuis le wizard.");
    const echec = (message: string) => {
      superadmin = null;
      discordError = message;
      o.log("Connexion Discord de l'Opérateur : échec.");
      return pageDiscord(res, 200, "Connexion Discord échouée", message);
    };
    if (params.get("error") === "access_denied")
      return echec("Vous avez refusé l'autorisation sur Discord. Relancez la connexion et acceptez-la : Shameless ne lit que votre identifiant.");
    const code = params.get("code");
    if (!code) return echec("Discord n'a pas renvoyé de code d'autorisation. Relancez la connexion depuis le wizard.");
    const r = await o.loginDiscord({ code, clientId: attendu.clientId, clientSecret: attendu.clientSecret, redirectUri: attendu.redirectUri });
    if (!r.ok)
      return echec(
        r.reason === "refused"
          ? `Discord a refusé la connexion. Vérifiez que l'URL de redirection ${attendu.redirectUri} figure bien dans OAuth2, Redirects, et que le Client ID et le Client Secret sont corrects.`
          : "Le wizard n'a pas pu joindre Discord. Vérifiez la connexion internet du serveur puis relancez la connexion.",
      );
    superadmin = { id: r.id, username: r.username };
    discordError = "";
    o.log("Connexion Discord de l'Opérateur : réussie.");
    return pageDiscord(res, 200, `Connecté en tant que ${r.username}`, "Ce compte deviendra le premier Superadministrateur de l'Instance.");
  }

  /** URL de retour OAuth du wizard : sur l'adresse publique, qui sert aussi le wizard via Caddy. */
  const redirectDuWizard = (corps: Record<string, unknown>) => callback(corps)?.replace("/api/auth/discord/callback", `/_setup/api/${CALLBACK}`) ?? null;

  const callback = (corps: Record<string, unknown>) => {
    const r = validateConfig({ publicUrl: corps.publicUrl });
    return r.errors.publicUrl ? null : `${new URL(String(corps.publicUrl).trim()).origin}/api/auth/discord/callback`;
  };

  function installer(value: Config) {
    // Les secrets internes sont générés une seule fois : la base est initialisée avec ce mot de passe.
    generated ??= { postgresPassword: tirer(ALNUM, 32) };
    // Idem pour les secrets Uchuu : le bloc déjà copié par l'Opérateur reste valide après une relance.
    if (value.uchuu) generated.uchuu ??= genererSecretsUchuu();
    recapUchuu = value.uchuu
      ? construireRecapUchuu({ adresseBot: value.botUrl, adressePublique: value.publicUrl, guildId: value.guildId, secrets: generated.uchuu! })
      : null;
    ecrireAtomique(join(o.instanceDir, "instance.env"), serializeEnv(buildInstanceEnv(value, generated)));
    ecrireAtomique(join(o.instanceDir, "ready"), "");
    publicUrl = value.publicUrl;
    failure = "";
    phase = "installing";
    o.log("Installation confirmée : configuration écrite, démarrage de l'Instance.");
    surveillerDemarrage();
  }

  function statique(req: IncomingMessage, res: ServerResponse) {
    if (!o.publicDir || (req.method !== "GET" && req.method !== "HEAD")) {
      res.writeHead(404);
      return res.end();
    }
    const chemin = normalize(decodeURIComponent((req.url ?? "/").split("?")[0])).replace(/^(\.\.[/\\])+/, "");
    let fichier = join(o.publicDir, chemin);
    if (!fichier.startsWith(o.publicDir) || !existsSync(fichier) || statSync(fichier).isDirectory()) fichier = join(o.publicDir, "index.html");
    const type = MIME[extname(fichier)] ?? "application/octet-stream";
    res.writeHead(200, {
      "content-type": type,
      "cache-control": fichier.endsWith("index.html") ? "no-store" : "public, max-age=3600",
      "x-content-type-options": "nosniff",
      "x-frame-options": "DENY",
      "referrer-policy": "no-referrer",
    });
    res.end(readFileSync(fichier));
  }

  const handler: RequestListener = (req, res) => {
    const url = req.url ?? "/";
    if (url.startsWith(API)) {
      api(req, res, url.slice(API.length).split("?")[0]).catch(() => {
        // Aucun détail de la requête n'est journalisé.
        o.log("Erreur interne du wizard.");
        if (!res.headersSent) repondre(res, 500, { message: "Erreur interne du wizard." });
      });
      return;
    }
    statique(req, res);
  };

  return { handler };
}
