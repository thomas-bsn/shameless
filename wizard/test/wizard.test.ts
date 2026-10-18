import assert from "node:assert/strict";
import { existsSync, mkdtempSync, readFileSync, statSync, writeFileSync } from "node:fs";
import { createServer, type Server } from "node:http";
import { tmpdir } from "node:os";
import { join } from "node:path";
import { afterEach, describe, it } from "node:test";
import { createWizard, isConfigured, type DiscordCheck, type DiscordLogin } from "../src/app.ts";

const saisie = {
  publicUrl: "https://shameless.example.org",
  clientId: "123456789012345678",
  clientSecret: "SECRET-DISCORD-abcdefghijklmnop",
  guildId: "987654321098765432",
};
const SUPERADMIN = "111111111111111111";

const serveurs: Server[] = [];
afterEach(() => {
  for (const s of serveurs.splice(0)) s.close();
});

async function demarrer(
  options: { discord?: DiscordCheck; apiUp?: () => boolean; installTimeoutMs?: number; ttlMs?: number } = {},
) {
  const dir = mkdtempSync(join(tmpdir(), "wizard-"));
  const logs: string[] = [];
  let maintenant = 1_000_000;
  const etat = {
    apiUp: false,
    fini: 0,
    discord: options.discord ?? ("ok" as DiscordCheck),
    login: { ok: true, id: SUPERADMIN, username: "thomas" } as DiscordLogin,
    echanges: [] as { code: string; clientId: string; clientSecret: string; redirectUri: string }[],
  };
  const wizard = createWizard({
    instanceDir: dir,
    log: (m) => logs.push(m),
    now: () => maintenant,
    codeTtlMs: options.ttlMs ?? 30 * 60_000,
    maxAttempts: 3,
    checkDiscord: async () => etat.discord,
    loginDiscord: async (demande) => (etat.echanges.push(demande), etat.login),
    probeApi: async () => etat.apiUp,
    pollIntervalMs: 5,
    installTimeoutMs: options.installTimeoutMs ?? 2000,
    finishGraceMs: 20,
    onFinished: () => etat.fini++,
  });
  const server = createServer(wizard.handler);
  serveurs.push(server);
  await new Promise<void>((r) => server.listen(0, "127.0.0.1", r));
  const base = `http://127.0.0.1:${(server.address() as { port: number }).port}`;
  let cookie = "";

  const appel = async (method: string, chemin: string, corps?: unknown, entetes: Record<string, string> = {}) => {
    const res = await fetch(base + chemin, {
      method,
      headers: { "content-type": "application/json", ...(cookie ? { cookie } : {}), ...entetes },
      body: corps === undefined ? undefined : JSON.stringify(corps),
    });
    const set = res.headers.get("set-cookie");
    if (set) cookie = set.split(";")[0];
    const texte = await res.text();
    return { status: res.status, headers: res.headers, json: texte && texte.startsWith("{") ? JSON.parse(texte) : undefined, texte };
  };
  const code = () => /Code d'accès : (\S+)/.exec(logs.join("\n").split("\n").reverse().find((l) => l.includes("Code d'accès")) ?? "")?.[1] ?? "";
  /** Démarre la connexion Discord (l'Opérateur clique) et renvoie l'URL d'autorisation et le state. */
  const demarrerConnexion = async (corps: unknown = saisie) => {
    const r = await appel("POST", "/_setup/api/discord/start", corps);
    const state = r.json?.authorizeUrl ? new URL(r.json.authorizeUrl).searchParams.get("state") : null;
    return { ...r, state };
  };
  /** Discord renvoie l'Opérateur sur le callback : navigation sans cookie de session (SameSite=Strict). */
  const retourDiscord = async (query: string) => {
    const res = await fetch(`${base}/_setup/api/discord/callback?${query}`, { redirect: "manual" });
    return { status: res.status, texte: await res.text(), headers: res.headers };
  };
  const connecter = async () => {
    const d = await demarrerConnexion();
    assert.equal(d.status, 200);
    const r = await retourDiscord(`code=abc&state=${d.state}`);
    assert.equal(r.status, 200);
  };
  const ouvrirSession = async ({ connexion = true } = {}) => {
    const r = await appel("POST", "/_setup/api/unlock", { code: code() });
    assert.equal(r.status, 200);
    if (connexion) await connecter();
  };
  const attendre = async (phase: string) => {
    for (let i = 0; i < 200; i++) {
      const r = await appel("GET", "/_setup/api/state");
      if (r.json?.phase === phase) return r.json;
      await new Promise((r) => setTimeout(r, 10));
    }
    throw new Error(`phase ${phase} jamais atteinte`);
  };
  return { dir, logs, etat, appel, demarrerConnexion, retourDiscord, connecter, code, ouvrirSession, attendre, avancer: (ms: number) => (maintenant += ms), resetCookie: () => (cookie = "") };
}

describe("accès protégé par un code", () => {
  it("affiche le code dans les logs et verrouille tout sans session", async () => {
    const w = await demarrer();
    assert.match(w.code(), /^[A-Z0-9]{4}(-[A-Z0-9]{4}){3}$/);
    assert.equal((await w.appel("GET", "/_setup/api/state")).json.phase, "locked");
    assert.equal((await w.appel("POST", "/_setup/api/install", saisie)).status, 401);
    assert.equal((await w.appel("POST", "/_setup/api/validate", saisie)).status, 401);
  });

  it("refuse un mauvais code puis ouvre la session avec le bon, une seule fois", async () => {
    const w = await demarrer();
    assert.equal((await w.appel("POST", "/_setup/api/unlock", { code: "AAAA-AAAA-AAAA-AAAA" })).status, 401);
    const ok = await w.appel("POST", "/_setup/api/unlock", { code: w.code().toLowerCase() });
    assert.equal(ok.status, 200);
    assert.match(ok.headers.get("set-cookie") ?? "", /HttpOnly/);
    assert.equal((await w.appel("GET", "/_setup/api/state")).json.phase, "configuring");

    // Usage unique : le même code ne rouvre pas une seconde session.
    w.resetCookie();
    assert.equal((await w.appel("POST", "/_setup/api/unlock", { code: w.code() })).status, 401);
  });

  it("refuse le code au bout de 30 minutes", async () => {
    const w = await demarrer();
    w.avancer(30 * 60_000 + 1);
    const r = await w.appel("POST", "/_setup/api/unlock", { code: w.code() });
    assert.equal(r.status, 410);
    assert.match(r.json.message, /expiré/);
  });

  it("remplace le code après trop d'essais ratés", async () => {
    const w = await demarrer();
    const ancien = w.code();
    for (let i = 0; i < 3; i++) await w.appel("POST", "/_setup/api/unlock", { code: "ZZZZ-ZZZZ-ZZZZ-ZZZZ" });
    assert.notEqual(w.code(), ancien);
    assert.equal((await w.appel("POST", "/_setup/api/unlock", { code: ancien })).status, 401);
    assert.equal((await w.appel("POST", "/_setup/api/unlock", { code: w.code() })).status, 200);
  });
});

describe("saisie et installation", () => {
  it("renvoie les erreurs de validation champ par champ sans rien écrire", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    const r = await w.appel("POST", "/_setup/api/install", { ...saisie, clientId: "abc", publicUrl: "http://a.example" });
    assert.equal(r.status, 422);
    assert.ok(r.json.errors.clientId && r.json.errors.publicUrl);
    assert.equal(existsSync(join(w.dir, "ready")), false);
    assert.equal(existsSync(join(w.dir, "instance.env")), false);
  });

  it("valide un champ à la volée et expose le callback calculé", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    const r = await w.appel("POST", "/_setup/api/validate", { ...saisie, guildId: "1" });
    assert.equal(r.status, 200);
    assert.deepEqual(Object.keys(r.json.errors), ["guildId"]);
    assert.equal(r.json.callbackUrl, "https://shameless.example.org/api/auth/discord/callback");
    assert.equal(r.json.wizardRedirectUri, "https://shameless.example.org/_setup/api/discord/callback");
  });

  it("explique un refus de Discord et laisse corriger", async () => {
    const w = await demarrer({ discord: "refused" });
    await w.ouvrirSession();
    const r = await w.appel("POST", "/_setup/api/install", saisie);
    assert.equal(r.status, 422);
    assert.match(r.json.errors.clientSecret, /Discord/);
    assert.equal(existsSync(join(w.dir, "ready")), false);
  });

  it("installe sans bloquer quand Discord est injoignable (simple avertissement)", async () => {
    const w = await demarrer({ discord: "unreachable" });
    await w.ouvrirSession();
    const r = await w.appel("POST", "/_setup/api/install", saisie);
    assert.equal(r.status, 202);
    assert.ok(r.json.warnings.length > 0);
  });

  it("écrit la configuration puis le marqueur, démarre, et s'arrête une fois l'API prête", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    const r = await w.appel("POST", "/_setup/api/install", saisie);
    assert.equal(r.status, 202);

    const env = readFileSync(join(w.dir, "instance.env"), "utf8");
    assert.match(env, /Auth__Discord__ClientId='123456789012345678'/);
    assert.match(env, /Auth__Discord__RedirectUri='https:\/\/shameless\.example\.org\/api\/auth\/discord\/callback'/);
    assert.match(env, /POSTGRES_PASSWORD='[A-Za-z0-9]{32,}'/);
    assert.ok(existsSync(join(w.dir, "ready")));
    assert.equal(statSync(join(w.dir, "instance.env")).mode & 0o022, 0, "non inscriptible par les autres");

    assert.equal((await w.attendre("installing")).phase, "installing");
    assert.equal(w.etat.fini, 0);
    w.etat.apiUp = true;
    const fin = await w.attendre("done");
    assert.equal(fin.publicUrl, "https://shameless.example.org");
    await new Promise((r) => setTimeout(r, 60));
    assert.equal(w.etat.fini, 1, "le wizard s'arrête");
  });

  it("n'écrit ni la saisie ni les secrets générés dans les logs, ni ne les relit après coup", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    await w.appel("POST", "/_setup/api/install", saisie);
    w.etat.apiUp = true;
    const fin = await w.attendre("done");
    const motDePasse = /POSTGRES_PASSWORD='([^']+)'/.exec(readFileSync(join(w.dir, "instance.env"), "utf8"))![1];
    const journal = w.logs.join("\n");
    for (const secret of [saisie.clientSecret, motDePasse, saisie.clientId, saisie.guildId]) {
      assert.ok(!journal.includes(secret), `le journal contient « ${secret} »`);
      assert.ok(!JSON.stringify(fin).includes(secret), "l'état renvoie un secret");
    }
  });

  it("signale l'échec du démarrage puis permet de corriger et réessayer avec le même mot de passe de base", async () => {
    const w = await demarrer({ installTimeoutMs: 80 });
    await w.ouvrirSession();
    await w.appel("POST", "/_setup/api/install", saisie);
    const echec = await w.attendre("failed");
    assert.match(echec.failure, /docker compose logs/);
    const avant = readFileSync(join(w.dir, "instance.env"), "utf8").match(/POSTGRES_PASSWORD='([^']+)'/)![1];

    w.etat.apiUp = true;
    const r = await w.appel("POST", "/_setup/api/install", { ...saisie, guildId: "555555555555555555" });
    assert.equal(r.status, 202);
    await w.attendre("done");
    const apres = readFileSync(join(w.dir, "instance.env"), "utf8");
    assert.match(apres, /AllowedGuildId='555555555555555555'/);
    assert.equal(apres.match(/POSTGRES_PASSWORD='([^']+)'/)![1], avant);
  });

  it("ne rouvre aucune session une fois l'installation terminée", async () => {
    const w = await demarrer();
    const code = w.code();
    await w.ouvrirSession();
    await w.appel("POST", "/_setup/api/install", saisie);
    w.etat.apiUp = true;
    await w.attendre("done");
    w.resetCookie();
    assert.equal((await w.appel("POST", "/_setup/api/unlock", { code })).status, 401);
    assert.equal((await w.appel("POST", "/_setup/api/install", saisie)).status, 401);
  });
});

describe("connexion Discord du premier Superadministrateur (#113)", () => {
  const env = (dir: string) => readFileSync(join(dir, "instance.env"), "utf8");

  it("calcule l'URL de redirection du wizard depuis l'adresse publique et demande le scope identify", async () => {
    const w = await demarrer();
    await w.ouvrirSession({ connexion: false });
    const r = await w.demarrerConnexion();
    assert.equal(r.status, 200);
    assert.equal(r.json.redirectUri, "https://shameless.example.org/_setup/api/discord/callback");
    const u = new URL(r.json.authorizeUrl);
    assert.equal(u.origin + u.pathname, "https://discord.com/oauth2/authorize");
    assert.equal(u.searchParams.get("client_id"), saisie.clientId);
    assert.equal(u.searchParams.get("scope"), "identify");
    assert.equal(u.searchParams.get("response_type"), "code");
    assert.equal(u.searchParams.get("redirect_uri"), r.json.redirectUri);
    assert.ok((r.state ?? "").length >= 32);
  });

  it("exige la session du wizard pour démarrer et refuse une adresse ou un Client ID invalide", async () => {
    const w = await demarrer();
    assert.equal((await w.demarrerConnexion()).status, 401);
    await w.ouvrirSession({ connexion: false });
    const r = await w.demarrerConnexion({ ...saisie, clientId: "abc", publicUrl: "pas une url" });
    assert.equal(r.status, 422);
    assert.ok(r.json.errors.clientId && r.json.errors.publicUrl);
  });

  it("enregistre l'identifiant Discord obtenu et l'affiche dans l'état, sans le secret ni le jeton", async () => {
    const w = await demarrer();
    await w.ouvrirSession({ connexion: false });
    assert.equal((await w.appel("GET", "/_setup/api/state")).json.discord, undefined);
    const d = await w.demarrerConnexion();
    const r = await w.retourDiscord(`code=le-code&state=${d.state}`);
    assert.equal(r.status, 200);
    assert.match(r.texte, /thomas/);
    assert.deepEqual(w.etat.echanges, [
      { code: "le-code", clientId: saisie.clientId, clientSecret: saisie.clientSecret, redirectUri: "https://shameless.example.org/_setup/api/discord/callback" },
    ]);
    const etat = (await w.appel("GET", "/_setup/api/state")).json;
    assert.deepEqual(etat.discord, { id: SUPERADMIN, username: "thomas" });
    assert.ok(!JSON.stringify(etat).includes(saisie.clientSecret));
  });

  it("écrit l'identifiant connecté comme premier Superadministrateur (Auth__AdminDiscordIds)", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    assert.equal((await w.appel("POST", "/_setup/api/install", saisie)).status, 202);
    assert.match(env(w.dir), new RegExp(`^Auth__AdminDiscordIds='${SUPERADMIN}'$`, "m"));
  });

  it("refuse d'installer sans connexion Discord, en l'expliquant, et n'écrit rien", async () => {
    const w = await demarrer();
    await w.ouvrirSession({ connexion: false });
    const r = await w.appel("POST", "/_setup/api/install", saisie);
    assert.equal(r.status, 422);
    assert.match(r.json.errors.discord, /Connectez-vous avec Discord/);
    assert.equal(existsSync(join(w.dir, "ready")), false);
    assert.equal(existsSync(join(w.dir, "instance.env")), false);
  });

  it("ignore un identifiant d'administrateur saisi à la main", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    await w.appel("POST", "/_setup/api/install", { ...saisie, adminIds: "222222222222222222" });
    assert.match(env(w.dir), new RegExp(`^Auth__AdminDiscordIds='${SUPERADMIN}'$`, "m"));
  });

  it("explique un refus de l'Opérateur sur Discord et permet de réessayer", async () => {
    const w = await demarrer();
    await w.ouvrirSession({ connexion: false });
    const d = await w.demarrerConnexion();
    const r = await w.retourDiscord(`error=access_denied&error_description=The+resource+owner+denied&state=${d.state}`);
    assert.equal(r.status, 200);
    assert.match(r.texte, /refusé/);
    const etat = (await w.appel("GET", "/_setup/api/state")).json;
    assert.equal(etat.discord, undefined);
    assert.match(etat.discordError, /refusé/);

    // Réessai : une nouvelle connexion réussit et efface l'erreur.
    await w.connecter();
    const apres = (await w.appel("GET", "/_setup/api/state")).json;
    assert.equal(apres.discordError, undefined);
    assert.equal(apres.discord.id, SUPERADMIN);
  });

  it("explique l'échec de l'échange (identifiants refusés ou Discord injoignable) et permet de réessayer", async () => {
    const w = await demarrer();
    await w.ouvrirSession({ connexion: false });
    for (const [login, motif] of [
      [{ ok: false, reason: "refused" }, /redirection|Client Secret/],
      [{ ok: false, reason: "unreachable" }, /joindre Discord/],
    ] as const) {
      w.etat.login = login;
      const d = await w.demarrerConnexion();
      await w.retourDiscord(`code=x&state=${d.state}`);
      const etat = (await w.appel("GET", "/_setup/api/state")).json;
      assert.equal(etat.discord, undefined);
      assert.match(etat.discordError, motif);
    }
    w.etat.login = { ok: true, id: SUPERADMIN, username: "thomas" };
    await w.connecter();
    assert.equal((await w.appel("POST", "/_setup/api/install", saisie)).status, 202);
  });

  it("refuse un state inconnu ou déjà utilisé, sans appeler Discord", async () => {
    const w = await demarrer();
    await w.ouvrirSession({ connexion: false });
    assert.equal((await w.retourDiscord("code=x&state=inconnu")).status, 400);
    const d = await w.demarrerConnexion();
    await w.retourDiscord(`code=x&state=${d.state}`);
    assert.equal((await w.retourDiscord(`code=x&state=${d.state}`)).status, 400);
    assert.equal(w.etat.echanges.length, 1);
  });

  it("refuse un state périmé (10 minutes)", async () => {
    const w = await demarrer();
    await w.ouvrirSession({ connexion: false });
    const d = await w.demarrerConnexion();
    w.avancer(10 * 60_000 + 1);
    const r = await w.retourDiscord(`code=x&state=${d.state}`);
    assert.equal(r.status, 400);
    assert.match(r.texte, /expiré/);
    assert.equal(w.etat.echanges.length, 0);
  });

  it("ne journalise ni l'identifiant Discord, ni le pseudo, ni le code, ni le state", async () => {
    const w = await demarrer();
    await w.ouvrirSession({ connexion: false });
    const d = await w.demarrerConnexion();
    await w.retourDiscord(`code=code-secret-xyz&state=${d.state}`);
    const journal = w.logs.join("\n");
    for (const s of [SUPERADMIN, "thomas", "code-secret-xyz", String(d.state)]) assert.ok(!journal.includes(s), s);
  });

  it("ne sert plus de callback une fois l'installation terminée", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    await w.appel("POST", "/_setup/api/install", saisie);
    w.etat.apiUp = true;
    await w.attendre("done");
    await new Promise((r) => setTimeout(r, 60));
    const d = await w.demarrerConnexion();
    assert.equal(d.status, 401);
  });
});

describe("options et intégration Uchuu (#71)", () => {
  const avecUchuu = { ...saisie, riotKey: "RGAPI-abc-123", timezone: "Europe/Brussels", uchuu: true, botUrl: "https://bot.example.org" };
  const lire = (dir: string) => Object.fromEntries([...readFileSync(join(dir, "instance.env"), "utf8").matchAll(/^(\w+)='(.*)'$/gm)].map((m) => [m[1], m[2]]));

  it("installe avec toutes les options désactivées, sans variable Uchuu ni recap", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    const r = await w.appel("POST", "/_setup/api/install", saisie);
    assert.equal(r.status, 202);
    assert.equal(r.json.uchuu, undefined);
    const env = lire(w.dir);
    assert.equal(env.RIOT_API_KEY, "");
    assert.equal(env.SHAMELESS_TIMEZONE, "Europe/Paris");
    assert.equal(env.SHAMELESS_BOT_API_KEY, undefined);
  });

  it("valide les options à la volée, avec avertissement pour un bot local", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    const r = await w.appel("POST", "/_setup/api/validate", { ...saisie, riotKey: "faux", uchuu: true, botUrl: "http://localhost:9000" });
    assert.deepEqual(Object.keys(r.json.errors), ["riotKey"]);
    assert.match(r.json.warnings[0], /injoignable depuis le conteneur de l'API/);
    assert.ok(!JSON.stringify(r.json).includes("faux"));
  });

  it("génère trois secrets distincts, les écrit dans instance.env et renvoie le récapitulatif à la confirmation", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    const r = await w.appel("POST", "/_setup/api/install", avecUchuu);
    assert.equal(r.status, 202);
    const env = lire(w.dir);
    assert.equal(env.RIOT_API_KEY, "RGAPI-abc-123");
    assert.equal(env.SHAMELESS_TIMEZONE, "Europe/Brussels");
    assert.equal(env.SHAMELESS_BOT_URL, "https://bot.example.org");
    assert.equal(env.SHAMELESS_WEB_URL, "https://shameless.example.org");
    const secrets = [env.SHAMELESS_BOT_API_KEY, env.SHAMELESS_WEBHOOK_SECRET, env.SHAMELESS_BOT_TO_API_KEY];
    assert.equal(new Set(secrets).size, 3);
    for (const s of secrets) assert.match(s, /^[0-9a-f]{64}$/);

    const bot = r.json.uchuu.variables.filter((v: { composant: string }) => v.composant === "Uchuu");
    assert.equal(bot.find((v: { nom: string }) => v.nom === "SHAMELESS_BOT_API_KEY").valeur, env.SHAMELESS_BOT_API_KEY);
    assert.equal(bot.find((v: { nom: string }) => v.nom === "SHAMELESS_GUILD_ID").valeur, saisie.guildId);
    assert.equal(bot.find((v: { nom: string }) => v.nom === "SHAMELESS_API_URL").valeur, "https://shameless.example.org/api");
    // Le récapitulatif reste lisible par la session tant que le wizard tourne (page rechargée, Caddy relancé).
    assert.deepEqual((await w.appel("GET", "/_setup/api/state")).json.uchuu, r.json.uchuu);
  });

  it("réutilise les mêmes secrets aux relances après un échec, même si l'adresse du bot change", async () => {
    const w = await demarrer({ installTimeoutMs: 80 });
    await w.ouvrirSession();
    await w.appel("POST", "/_setup/api/install", avecUchuu);
    const avant = lire(w.dir);
    await w.attendre("failed");
    w.etat.apiUp = true;
    const r = await w.appel("POST", "/_setup/api/install", { ...avecUchuu, botUrl: "https://bot2.example.org" });
    assert.equal(r.status, 202);
    const apres = lire(w.dir);
    assert.equal(apres.SHAMELESS_BOT_URL, "https://bot2.example.org");
    for (const k of ["SHAMELESS_BOT_API_KEY", "SHAMELESS_WEBHOOK_SECRET", "SHAMELESS_BOT_TO_API_KEY"]) assert.equal(apres[k], avant[k], k);
  });

  it("n'écrit aucun secret Uchuu ni la clé Riot dans les logs", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    await w.appel("POST", "/_setup/api/install", avecUchuu);
    w.etat.apiUp = true;
    await w.attendre("done");
    const env = lire(w.dir);
    const journal = w.logs.join("\n");
    for (const s of [env.SHAMELESS_BOT_API_KEY, env.SHAMELESS_WEBHOOK_SECRET, env.SHAMELESS_BOT_TO_API_KEY, "RGAPI-abc-123"])
      assert.ok(!journal.includes(s), "secret dans le journal");
  });

  it("n'appelle jamais le bot", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    const fetchOriginal = globalThis.fetch;
    const urls: string[] = [];
    globalThis.fetch = ((u: string, i?: RequestInit) => (urls.push(String(u)), fetchOriginal(u, i))) as typeof fetch;
    try {
      await w.appel("POST", "/_setup/api/validate", avecUchuu);
      await w.appel("POST", "/_setup/api/install", avecUchuu);
    } finally {
      globalThis.fetch = fetchOriginal;
    }
    assert.ok(urls.every((u) => u.startsWith("http://127.0.0.1")), urls.join(","));
  });
});

describe("instance déjà configurée", () => {
  it("est détectée par le marqueur ready, pas par la seule présence de instance.env", () => {
    const dir = mkdtempSync(join(tmpdir(), "wizard-"));
    assert.equal(isConfigured(dir), false);
    writeFileSync(join(dir, "instance.env"), "A='1'\n");
    assert.equal(isConfigured(dir), false);
    writeFileSync(join(dir, "ready"), "");
    assert.equal(isConfigured(dir), true);
  });
});

describe("HTTPS terminé par un proxy en amont (Forgeyard, Traefik)", () => {
  const adresseServie = (w: Awaited<ReturnType<typeof demarrer>>) =>
    /^SHAMELESS_SITE_ADDRESS='(.*)'$/m.exec(readFileSync(join(w.dir, "instance.env"), "utf8"))?.[1];

  it("fait servir Caddy en HTTP quand la page arrive en HTTPS par le port 80", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    const r = await w.appel("POST", "/_setup/api/install", saisie, { "x-shameless-entry": "http", "x-forwarded-proto": "https" });
    assert.equal(r.status, 202);
    assert.equal(adresseServie(w), ":80");
    assert.ok(r.json.warnings.some((m: string) => /proxy en amont/.test(m)));
  });

  it("détecte le HTTPS en amont par l'Origin quand X-Forwarded-Proto n'est pas transmis", async () => {
    const w = await demarrer();
    await w.ouvrirSession();
    const r = await w.appel("POST", "/_setup/api/install", saisie, { "x-shameless-entry": "http", origin: "https://shameless.example.org" });
    assert.equal(r.status, 202);
    assert.equal(adresseServie(w), ":80");
  });

  it("laisse Caddy gérer le HTTPS d'un domaine joint directement", async () => {
    for (const entetes of [{} as Record<string, string>, { "x-shameless-entry": "https", "x-forwarded-proto": "https" }, { "x-shameless-entry": "http" }, { "x-shameless-entry": "http", origin: "http://shameless.example.org" }]) {
      const w = await demarrer();
      await w.ouvrirSession();
      assert.equal((await w.appel("POST", "/_setup/api/install", saisie, entetes)).status, 202);
      assert.equal(adresseServie(w), "shameless.example.org", JSON.stringify(entetes));
    }
  });
});
