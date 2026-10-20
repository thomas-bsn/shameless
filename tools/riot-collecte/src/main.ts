import { parseArgs } from "node:util";
import { collecter } from "./collecte.ts";
import { lireCle } from "./cle.ts";

const AIDE = `Usage : node src/main.ts NOM#TAG --dossier DOSSIER [--nombre 100] [--region euw]

  NOM#TAG      RiotId du joueur ciblé
  --dossier    dossier de sortie (créé au besoin ; une relance reprend où elle s'est arrêtée)
  --nombre     nombre de games classées (SoloQ) les plus récentes (défaut 100)
  --region     euw (défaut), eune, na, kr, ...
  --base-url   racine de l'API, pour tester contre un faux serveur (local uniquement)

La clé Riot est lue dans la variable RIOT_API_KEY, sinon dans ~/.config/shameless/riot-key.`;

function baseUrlAcceptable(url: string): boolean {
  try {
    const u = new URL(url);
    if (u.protocol === "https:" && u.hostname.endsWith(".api.riotgames.com")) return true;
    return u.protocol === "http:" && ["127.0.0.1", "localhost", "[::1]"].includes(u.hostname);
  } catch {
    return false;
  }
}

async function main(): Promise<void> {
  const { values, positionals } = parseArgs({
    allowPositionals: true,
    options: {
      dossier: { type: "string" },
      nombre: { type: "string", default: "100" },
      region: { type: "string", default: "euw" },
      "base-url": { type: "string" },
      help: { type: "boolean", short: "h" },
    },
  });
  if (values.help) return console.log(AIDE);
  const riotId = positionals[0];
  const nombre = Number(values.nombre);
  if (!riotId || !values.dossier || !Number.isInteger(nombre) || nombre < 1) {
    console.error(AIDE);
    process.exitCode = 2;
    return;
  }
  const baseUrl = values["base-url"];
  if (baseUrl && !baseUrlAcceptable(baseUrl)) {
    throw new Error("--base-url refusée : la clé ne part que vers l'API Riot ou vers localhost.");
  }
  const cle = await lireCle();
  await collecter({ riotId, nombre, dossier: values.dossier, cle, baseUrl, region: values.region });
}

main().catch((e: unknown) => {
  console.error(`Erreur : ${e instanceof Error ? e.message : String(e)}`);
  process.exitCode = 1;
});
