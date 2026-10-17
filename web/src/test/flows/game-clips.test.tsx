import { screen, within } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const recap: Schemas["GameRecapResponse"] = {
  id: 42,
  seriesId: 5,
  gameNumber: 1,
  bestOf: 1,
  status: "completed",
  playedAt: "2026-10-16T20:30:00Z",
  completedAt: "2026-10-16T21:05:00Z",
  teamASide: "blue",
  teamBSide: "red",
  winner: "A",
  teamAWins: 1,
  teamBWins: 0,
  seriesOutcome: "Gagnee",
  riotMatchId: null,
  match: null,
  teamA: [],
  teamB: [],
};

const clip = (id: number, overrides: Partial<Schemas["ClipResponse"]> = {}): Schemas["ClipResponse"] => ({
  id,
  gameId: 42,
  rattachement: "automatique",
  auteurNom: "Sam",
  auteurDiscordId: "111111111111111111",
  publieLe: "2026-10-16T21:10:00Z",
  nomFichier: "pentakill.mp4",
  contentType: "video/mp4",
  taille: 12_000_000,
  ...overrides,
});

describe("Clips sur la page d'une Game", () => {
  it("lit chaque clip de la Game depuis le stockage de l'Instance, avec son auteur", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap,
      "GET /games/42/clips": [clip(7), clip(8, { auteurNom: "Léa", nomFichier: "baron.webm", contentType: "video/webm" })],
    });

    renderApp("/games/42");

    const clips = await screen.findByRole("region", { name: "Clips" });
    const premier = await within(clips).findByRole("figure", { name: "pentakill.mp4" });
    expect(within(premier).getByText(/Sam/)).toBeInTheDocument();
    const video = premier.querySelector("video");
    expect(video).toHaveAttribute("src", "/api/clips/7/fichier");
    expect(video).toHaveAttribute("controls");
    expect(within(clips).getByRole("figure", { name: "baron.webm" })).toHaveTextContent("Léa");
  });

  it("dit quand la Game n'a pas encore de clip", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/42": recap, "GET /games/42/clips": [] });

    renderApp("/games/42");

    const clips = await screen.findByRole("region", { name: "Clips" });
    expect(await within(clips).findByText(/Aucun clip/)).toBeInTheDocument();
  });
});
