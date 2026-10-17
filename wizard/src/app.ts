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
  let phase: "locked" | "configuring" | "installing" | "done" | "failed" = "locked";
  let publicUrl = "";
  let failure = "";
  let warnings: string[] = [];
  let generated: Generated | null = null;
  // Récapitulatif Uchuu (contient les secrets) : en mémoire seulement, jamais journalisé.
  let recapUchuu: RecapUchuu | null = null;
  let termine = false;

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
    if (!sessionValide(req)) return { phase: "locked" as Phase, codeExpired: !codeUtilise && o.now() > codeEmisA + o.codeTtlMs };
    return {
      phase: phase as Phase,
      ...(publicUrl ? { publicUrl } : {}),
      ...(phase === "failed" ? { failure } : {}),
      ...(warnings.length > 0 ? { warnings } : {}),
      ...(recapUchuu ? { uchuu: recapUchuu } : {}),
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
      return repondre(res, 200, { errors, warnings, callbackUrl: callback(corps) });
    }

    if (route === "install") {
      if (phase !== "configuring" && phase !== "failed") return repondre(res, 409, { message: "Une installation est déjà en cours." });
      const verdict = validateConfig(corps);
      const { errors, value } = verdict;
      if (!value) return repondre(res, 422, { errors });

      warnings = [...verdict.warnings];
      const discord = await o.checkDiscord(value.clientId, value.clientSecret);
      if (discord === "refused")
        return repondre(res, 422, {
          errors: { clientSecret: "Discord refuse ce Client ID / Client Secret. Vérifiez-les dans le portail développeur (page OAuth2) ; au besoin, « Reset Secret »." },
        });
      if (discord === "unreachable")
        warnings.push("Discord n'a pas pu être joint pour vérifier ces identifiants : ils seront testés au premier démarrage.");

      installer(value);
      return repondre(res, 202, { phase, warnings, ...(recapUchuu ? { uchuu: recapUchuu } : {}) });
    }

    return repondre(res, 404, { message: "Inconnu." });
  }

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
