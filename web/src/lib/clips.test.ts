import type { Schemas } from "@/api/client";
import { grouperParSoiree, libelleDeGame, tailleLabel } from "./clips";

const game = (gameId: number, lanceeLe: string, overrides: Partial<Schemas["GameDeLaListeDeClips"]> = {}): Schemas["GameDeLaListeDeClips"] => ({
  gameId,
  seriesId: 2,
  gameNumber: gameId,
  bestOf: 3,
  lanceeLe,
  scoreA: 1,
  scoreB: 0,
  ...overrides,
});

const clip = (id: number, g: Schemas["GameDeLaListeDeClips"]): Schemas["ClipDeLaListe"] => ({
  id,
  gameId: g.gameId,
  rattachement: "automatique",
  auteurNom: "Sam",
  auteurDiscordId: "1",
  publieLe: g.lanceeLe,
  nomFichier: "a.mp4",
  contentType: "video/mp4",
  taille: 1,
  masqueDuFond: false,
  game: g,
});

describe("grouperParSoiree", () => {
  it("range les clips par soirée puis par game, les plus récentes d'abord", () => {
    const g1 = game(1, "2026-10-16T12:00:00Z");
    const g2 = game(2, "2026-10-17T12:00:00Z");
    const g3 = game(3, "2026-10-17T14:00:00Z");

    const soirees = grouperParSoiree([clip(10, g1), clip(11, g2), clip(12, g3), clip(13, g2)]);

    expect(soirees.map((s) => s.jour)).toEqual(["2026-10-17", "2026-10-16"]);
    expect(soirees[0].games.map((g) => g.game.gameId)).toEqual([3, 2]);
    expect(soirees[0].games[1].clips.map((c) => c.id)).toEqual([11, 13]);
  });

  it("ignore les clips sans game", () => {
    expect(grouperParSoiree([{ ...clip(1, game(1, "2026-10-16T12:00:00Z")), game: null }])).toEqual([]);
  });
});

describe("libelleDeGame", () => {
  it("dit la série et le rang, ou le numéro d'une game hors série", () => {
    expect(libelleDeGame(game(7, "2026-10-16T12:00:00Z", { seriesId: 5, gameNumber: 2 }))).toBe("Série 5 · Game 2");
    expect(libelleDeGame(game(7, "2026-10-16T12:00:00Z", { seriesId: null }))).toBe("Game #7");
  });
});

describe("tailleLabel", () => {
  it("arrondit en Mo", () => {
    expect(tailleLabel(17_400_000)).toBe("17 Mo");
    expect(tailleLabel(200_000)).toBe("< 1 Mo");
  });
});
