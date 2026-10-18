import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const recap: Schemas["GameRecapResponse"] = {
  id: 42,
  seriesId: 5,
  gameNumber: 1,
  bestOf: 3,
  status: "completed",
  playedAt: "2026-10-16T20:00:00Z",
  completedAt: "2026-10-16T20:35:00Z",
  teamASide: "blue",
  teamBSide: "red",
  winner: "A",
  teamAWins: 1,
  teamBWins: 0,
  seriesOutcome: "EnCours",
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
  masqueDuFond: false,
  ...overrides,
});

const candidates: Schemas["GameCandidateDuClip"][] = [
  { gameId: 43, seriesId: 5, gameNumber: 2, bestOf: 3, lanceeLe: "2026-10-16T20:40:00Z" },
  { gameId: 42, seriesId: 5, gameNumber: 1, bestOf: 3, lanceeLe: "2026-10-16T20:00:00Z" },
];

const requetes = (api: ReturnType<typeof mockApi>, method: string) =>
  api.mock.calls.map(([r]) => r).filter((r) => r.method === method);

describe("Rattachement des clips depuis la page d'une Game", () => {
  it("retire un clip de la Game, qui ne l'affiche plus", async () => {
    let clips = [clip(7)];
    const api = mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap,
      "GET /games/42/clips": () => json(clips),
      "DELETE /clips/7/game": () => {
        clips = [];
        return json(clip(7, { gameId: null, rattachement: null }));
      },
    });

    renderApp("/games/42");

    const region = await screen.findByRole("region", { name: "Clips" });
    const figure = await within(region).findByRole("figure", { name: "pentakill.mp4" });
    await userEvent.click(within(figure).getByRole("button", { name: "Retirer de la Game" }));

    expect(await within(region).findByText(/Aucun clip/)).toBeInTheDocument();
    expect(requetes(api, "DELETE")[0]?.url).toMatch(/\/api\/clips\/7\/game$/);
  });

  it("corrige la Game d'un clip parmi les autres Games proposées", async () => {
    let clips = [clip(7)];
    const api = mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap,
      "GET /games/42/clips": () => json(clips),
      "GET /clips/7/games-candidates": candidates,
      "PUT /clips/7/game": () => {
        clips = [];
        return json(clip(7, { gameId: 43, rattachement: "manuel" }));
      },
    });

    renderApp("/games/42");

    const region = await screen.findByRole("region", { name: "Clips" });
    const figure = await within(region).findByRole("figure", { name: "pentakill.mp4" });
    await userEvent.click(within(figure).getByRole("button", { name: "Corriger la Game" }));
    const proposees = await within(figure).findByRole("group", { name: "Games proposées" });
    expect(within(proposees).queryByRole("button", { name: /Game 1 · BO3/ })).not.toBeInTheDocument();
    await userEvent.click(within(proposees).getByRole("button", { name: /Game 2 · BO3/ }));

    expect(await within(region).findByText(/Aucun clip/)).toBeInTheDocument();
    expect(await requetes(api, "PUT")[0]?.clone().json()).toEqual({ gameId: 43 });
  });

  it("dit qu'un clip a été rattaché à la main", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap,
      "GET /games/42/clips": [clip(7, { rattachement: "manuel" })],
    });

    renderApp("/games/42");

    const figure = await screen.findByRole("figure", { name: "pentakill.mp4" });
    expect(within(figure).getByText(/rattaché à la main/)).toBeInTheDocument();
  });
});
