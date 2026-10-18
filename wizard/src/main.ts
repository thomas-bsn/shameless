// Point d'entrée du conteneur wizard : lit l'environnement, câble les dépendances réelles, écoute.
import { mkdirSync } from "node:fs";
import { createServer } from "node:http";
import { createWizard, isConfigured, type DiscordCheck, type DiscordLogin } from "./app.ts";

const env = process.env;
const instanceDir = env.WIZARD_INSTANCE_DIR ?? "/instance";
const port = Number(env.WIZARD_PORT ?? 8080);
const apiHealthUrl = env.WIZARD_API_HEALTH_URL ?? "http://api:8080/health";
const log = (m: string) => console.log(`[wizard] ${m}`);

// Une Instance déjà configurée ne doit jamais rouvrir le wizard (sinon : prise de contrôle).
mkdirSync(instanceDir, { recursive: true });
if (isConfigured(instanceDir)) {
  log("L'Instance est déjà configurée : le wizard s'arrête.");
  process.exit(0);
}

async function checkDiscord(clientId: string, clientSecret: string): Promise<DiscordCheck> {
  if (env.WIZARD_DISCORD_CHECK === "off") return "unreachable";
  try {
    const res = await fetch("https://discord.com/api/v10/oauth2/token", {
      method: "POST",
      headers: {
        "content-type": "application/x-www-form-urlencoded",
        authorization: `Basic ${Buffer.from(`${clientId}:${clientSecret}`).toString("base64")}`,
      },
      body: "grant_type=client_credentials&scope=identify",
      signal: AbortSignal.timeout(8000),
    });
    if (res.ok) return "ok";
    return res.status === 401 ? "refused" : "unreachable";
  } catch {
    return "unreachable";
  }
}

/** Flux OAuth Discord (scope identify) : échange du code, puis lecture de l'identifiant. Le jeton n'est jamais conservé. */
async function loginDiscord(d: { code: string; clientId: string; clientSecret: string; redirectUri: string }): Promise<DiscordLogin> {
  // Réservé au test d'acceptation (aucun vrai compte Discord en CI) : l'identité est celle de la variable.
  if (env.WIZARD_DISCORD_LOGIN_FAKE_ID) return { ok: true, id: env.WIZARD_DISCORD_LOGIN_FAKE_ID, username: "operateur-de-test" };
  try {
    const jeton = await fetch("https://discord.com/api/v10/oauth2/token", {
      method: "POST",
      headers: { "content-type": "application/x-www-form-urlencoded" },
      body: new URLSearchParams({
        grant_type: "authorization_code",
        code: d.code,
        redirect_uri: d.redirectUri,
        client_id: d.clientId,
        client_secret: d.clientSecret,
      }),
      signal: AbortSignal.timeout(8000),
    });
    if (jeton.status >= 500) return { ok: false, reason: "unreachable" };
    if (!jeton.ok) return { ok: false, reason: "refused" };
    const { access_token } = (await jeton.json()) as { access_token?: string };
    if (!access_token) return { ok: false, reason: "refused" };
    const moi = await fetch("https://discord.com/api/v10/users/@me", {
      headers: { authorization: `Bearer ${access_token}` },
      signal: AbortSignal.timeout(8000),
    });
    if (!moi.ok) return { ok: false, reason: moi.status >= 500 ? "unreachable" : "refused" };
    const u = (await moi.json()) as { id?: string; username?: string; global_name?: string | null };
    return u.id ? { ok: true, id: u.id, username: u.global_name || u.username || u.id } : { ok: false, reason: "refused" };
  } catch {
    return { ok: false, reason: "unreachable" };
  }
}

const wizard = createWizard({
  instanceDir,
  publicDir: env.WIZARD_PUBLIC_DIR ?? "/app/public",
  log,
  now: Date.now,
  codeTtlMs: Number(env.WIZARD_CODE_TTL_SECONDS ?? 1800) * 1000,
  maxAttempts: 5,
  checkDiscord,
  loginDiscord,
  probeApi: async () => {
    const res = await fetch(apiHealthUrl, { signal: AbortSignal.timeout(3000) });
    return res.ok;
  },
  pollIntervalMs: 2000,
  installTimeoutMs: Number(env.WIZARD_INSTALL_TIMEOUT_SECONDS ?? 300) * 1000,
  finishGraceMs: 15_000,
  onFinished: () => process.exit(0),
});

const server = createServer(wizard.handler);
server.listen(port, "0.0.0.0", () => log(`À l'écoute sur le port ${port} (accessible via le proxy).`));
process.on("SIGTERM", () => process.exit(0));
