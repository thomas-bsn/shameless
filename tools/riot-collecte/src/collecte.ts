import { mkdir, readFile, rename, writeFile } from "node:fs/promises";
import { join } from "node:path";
import { RiotClient, type Limites } from "./client.ts";

export const QUEUE_SOLOQ = 420;
const TAILLE_PAGE = 100;

export interface OptionsCollecte {
  /** `NOM#TAG` */
  riotId: string;
  /** Nombre de games classées à enregistrer (les plus récentes). */
  nombre: number;
  dossier: string;
  cle: string;
  /** Racine de l'API ; par défaut le routage régional Riot. */
  baseUrl?: string;
  region?: string;
  limites?: Limites;
  journal?: (ligne: string) => void;
}

export interface EntreeIndex {
  matchId: string;
  date: string;
  duree: number;
  champion: string;
}

export interface Index {
  cible: { riotId: string; puuid: string };
  games: EntreeIndex[];
}

export interface ResultatCollecte {
  telechargees: number;
  dejaPresentes: number;
  ignorees: number;
}

const ID_VALIDE = /^[A-Za-z0-9]+_\d+$/;

export function analyserRiotId(riotId: string): { nom: string; tag: string } {
  const i = riotId.lastIndexOf("#");
  const nom = riotId.slice(0, i).trim();
  const tag = riotId.slice(i + 1).trim();
  if (i < 0 || !nom || !tag) throw new Error(`RiotId invalide : « ${riotId} » (attendu NOM#TAG)`);
  return { nom, tag };
}

async function ecrireAtomique(chemin: string, contenu: string): Promise<void> {
  const tmp = `${chemin}.tmp`;
  await writeFile(tmp, contenu);
  await rename(tmp, chemin);
}

async function lireJson<T>(chemin: string): Promise<T | undefined> {
  try {
    return JSON.parse(await readFile(chemin, "utf8")) as T;
  } catch {
    return undefined;
  }
}

function entreeIndex(doc: any, puuid: string): EntreeIndex | undefined {
  const info = doc?.info;
  const moi = info?.participants?.find((p: any) => p.puuid === puuid);
  if (!info || !moi) return undefined;
  return {
    matchId: doc.metadata.matchId,
    date: new Date(info.gameStartTimestamp ?? info.gameCreation).toISOString(),
    duree: info.gameDuration,
    champion: moi.championName,
  };
}

export async function collecter(o: OptionsCollecte): Promise<ResultatCollecte> {
  const journal = o.journal ?? ((l: string) => console.log(l));
  const { nom, tag } = analyserRiotId(o.riotId);
  const client = new RiotClient({ cle: o.cle, baseUrl: o.baseUrl, region: o.region, limites: o.limites, journal });

  const compte = await client.get<{ puuid: string }>(
    `/riot/account/v1/accounts/by-riot-id/${encodeURIComponent(nom)}/${encodeURIComponent(tag)}`,
  );
  if (!compte) throw new Error(`Compte introuvable : ${o.riotId}`);
  const puuid = compte.puuid;

  const dossierMatches = join(o.dossier, "matches");
  await mkdir(dossierMatches, { recursive: true });
  const cheminIndex = join(o.dossier, "index.json");
  const ancien = await lireJson<Index>(cheminIndex);
  const parId = new Map<string, EntreeIndex>();
  if (ancien && ancien.cible?.puuid === puuid) for (const g of ancien.games) parId.set(g.matchId, g);

  const resultat: ResultatCollecte = { telechargees: 0, dejaPresentes: 0, ignorees: 0 };
  const demandes: string[] = [];
  const ecrireIndex = async () => {
    const games = [...parId.values()].sort((a, b) => b.date.localeCompare(a.date));
    const index: Index = { cible: { riotId: `${nom}#${tag}`, puuid }, games };
    await ecrireAtomique(cheminIndex, JSON.stringify(index, null, 2) + "\n");
  };

  try {
    // Liste des identifiants, page par page.
    while (demandes.length < o.nombre) {
      const taille = Math.min(TAILLE_PAGE, o.nombre - demandes.length);
      const ids = await client.get<string[]>(
        `/lol/match/v5/matches/by-puuid/${encodeURIComponent(puuid)}/ids?queue=${QUEUE_SOLOQ}&start=${demandes.length}&count=${taille}`,
      );
      if (!ids || ids.length === 0) break;
      demandes.push(...ids);
      if (ids.length < taille) break;
    }
    journal(`${demandes.length} game(s) classée(s) à enregistrer pour ${nom}#${tag}`);

    for (const id of demandes) {
      if (!ID_VALIDE.test(id)) {
        journal(`Identifiant de game inattendu, ignoré : ${id}`);
        resultat.ignorees++;
        continue;
      }
      const chemin = join(dossierMatches, `${id}.json`);
      let doc = await lireJson<any>(chemin);
      if (doc) {
        resultat.dejaPresentes++;
      } else {
        doc = await client.get<any>(`/lol/match/v5/matches/${id}`);
        if (!doc) {
          journal(`Game ${id} introuvable, ignorée`);
          resultat.ignorees++;
          continue;
        }
        await ecrireAtomique(chemin, JSON.stringify(doc));
        resultat.telechargees++;
        journal(`Game ${id} enregistrée (${resultat.telechargees + resultat.dejaPresentes}/${demandes.length})`);
      }
      const e = entreeIndex(doc, puuid);
      if (e) parId.set(id, e);
      else journal(`Game ${id} : le joueur n'y figure pas, absent de l'index`);
    }
  } finally {
    await ecrireIndex();
  }
  journal(
    `Terminé : ${resultat.telechargees} téléchargée(s), ${resultat.dejaPresentes} déjà présente(s), ${resultat.ignorees} ignorée(s)`,
  );
  return resultat;
}
