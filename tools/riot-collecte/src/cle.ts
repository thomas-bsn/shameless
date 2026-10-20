import { readFile } from "node:fs/promises";
import { homedir } from "node:os";
import { join } from "node:path";

export const CHEMIN_CLE = () => join(homedir(), ".config", "shameless", "riot-key");

/** Variable `RIOT_API_KEY` d'abord, sinon le fichier `~/.config/shameless/riot-key`. */
export async function lireCle(env: NodeJS.ProcessEnv = process.env): Promise<string> {
  let cle = env.RIOT_API_KEY?.trim();
  if (!cle) {
    try {
      cle = (await readFile(CHEMIN_CLE(), "utf8")).trim();
    } catch {
      cle = undefined;
    }
  }
  if (!cle) {
    throw new Error(
      `Clé Riot introuvable : mettez-la dans le fichier ${CHEMIN_CLE()} ou dans la variable RIOT_API_KEY.`,
    );
  }
  if (/\s/.test(cle)) throw new Error("Clé Riot invalide : elle ne doit contenir ni espace ni retour à la ligne.");
  return cle;
}
