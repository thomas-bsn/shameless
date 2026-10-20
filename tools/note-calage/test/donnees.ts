import { mkdir, mkdtemp, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";
import type { Game, Participant } from "../src/types.ts";

export const T0 = Date.UTC(2026, 9, 1, 20, 0, 0);

const ROLES = ["TOP", "JUNGLE", "MIDDLE", "BOTTOM", "UTILITY"];

/** Un joueur de test : les stats non précisées sont celles d'un joueur moyen. */
export function joueur(champion: string, role: string, equipe: 100 | 200, kda: [number, number, number], reste: Partial<Participant> = {}): Participant {
  return {
    puuid: `puuid-${champion}`,
    championName: champion,
    teamId: equipe,
    teamPosition: role,
    individualPosition: role,
    win: equipe === 100,
    kills: kda[0],
    deaths: kda[1],
    assists: kda[2],
    totalMinionsKilled: 180,
    neutralMinionsKilled: 10,
    totalDamageDealtToChampions: 20000,
    visionScore: 25,
    damageDealtToObjectives: 8000,
    goldEarned: 11000,
    ...reste,
  };
}

/** Une game de dix joueurs, champions `<prefixe>0` à `<prefixe>9`, kills = i + 1 pour les distinguer. */
export function gameDeTest(matchId: string, debut: number, duree: number, prefixe: string): Game {
  const participants = Array.from({ length: 10 }, (_, i) =>
    joueur(`${prefixe}${i}`, ROLES[i % 5]!, i < 5 ? 100 : 200, [i + 1, 3, 4 + i]),
  );
  return { matchId, debut, duree, participants };
}

/** Écrit les games dans un dossier au format du script de collecte (`index.json`, `matches/<id>.json`). */
export async function ecrireDossier(games: Game[], ciblePuuid = "puuid-cible"): Promise<string> {
  const dossier = await mkdtemp(join(tmpdir(), "note-calage-"));
  await mkdir(join(dossier, "matches"));
  for (const g of games) {
    const doc = {
      metadata: { matchId: g.matchId },
      info: { gameStartTimestamp: g.debut, gameDuration: g.duree, queueId: 420, participants: g.participants },
    };
    await writeFile(join(dossier, "matches", `${g.matchId}.json`), JSON.stringify(doc));
  }
  const index = {
    cible: { riotId: "Sam#EUW", puuid: ciblePuuid },
    games: games.map((g) => ({
      matchId: g.matchId,
      date: new Date(g.debut).toISOString(),
      duree: g.duree,
      champion: g.participants[0]!.championName,
    })),
  };
  await writeFile(join(dossier, "index.json"), JSON.stringify(index));
  return dossier;
}
