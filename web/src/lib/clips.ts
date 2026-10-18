import type { Schemas } from "@/api/client";

type Clip = Schemas["ClipDeLaListe"];
type Game = Schemas["GameDeLaListeDeClips"];

export type GameDuFil = { game: Game; clips: Clip[] };
export type SoireeDuFil = { jour: string; games: GameDuFil[] };

/** Le jour local d'un instant, « 2026-10-17 » : la soirée d'une Game. */
const jourLocal = (iso: string) => new Date(iso).toLocaleDateString("sv-SE");

/**
 * Le fil des clips rattachés : par soirée (jour de lancement de la Game), puis par Game, les plus récentes
 * d'abord. Les clips sans Game n'y sont pas : ils vont dans « À rattacher ».
 */
export function grouperParSoiree(clips: Clip[]): SoireeDuFil[] {
  const parGame = new Map<number, GameDuFil>();
  for (const clip of clips) {
    if (clip.game == null) continue;
    const groupe = parGame.get(clip.game.gameId) ?? { game: clip.game, clips: [] };
    groupe.clips.push(clip);
    parGame.set(clip.game.gameId, groupe);
  }

  const parJour = new Map<string, GameDuFil[]>();
  for (const groupe of parGame.values()) {
    const jour = jourLocal(groupe.game.lanceeLe);
    parJour.set(jour, [...(parJour.get(jour) ?? []), groupe]);
  }

  return [...parJour.entries()]
    .sort(([a], [b]) => b.localeCompare(a))
    .map(([jour, games]) => ({
      jour,
      games: games.sort((a, b) => b.game.lanceeLe.localeCompare(a.game.lanceeLe) || b.game.gameId - a.game.gameId),
    }));
}

/** « Série 5 · Game 2 », ou « Game #7 » pour une Game hors Série. */
export const libelleDeGame = (game: Game) =>
  game.seriesId == null ? `Game #${game.gameId}` : `Série ${game.seriesId} · Game ${game.gameNumber}`;

/** Le poids d'une vidéo : « 17 Mo ». */
export const tailleLabel = (octets: number) => (octets < 1_000_000 ? "< 1 Mo" : `${Math.round(octets / 1_000_000)} Mo`);

/** « Soirée du 17 octobre » à partir de « 2026-10-17 ». */
export const soireeLabel = (jour: string) =>
  `Soirée du ${new Date(`${jour}T12:00:00`).toLocaleDateString("fr-FR", { day: "numeric", month: "long" })}`;
