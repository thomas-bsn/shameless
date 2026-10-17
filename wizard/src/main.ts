// Point d'entrée du conteneur wizard : lit l'environnement, câble les dépendances réelles, écoute.
import { mkdirSync } from "node:fs";
import { createServer } from "node:http";
import { createWizard, isConfigured, type DiscordCheck } from "./app.ts";

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

const wizard = createWizard({
  instanceDir,
  publicDir: env.WIZARD_PUBLIC_DIR ?? "/app/public",
  log,
  now: Date.now,
  codeTtlMs: Number(env.WIZARD_CODE_TTL_SECONDS ?? 1800) * 1000,
  maxAttempts: 5,
  checkDiscord,
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
