import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const clip = (id: number, overrides: Partial<Schemas["ClipResponse"]> = {}): Schemas["ClipResponse"] => ({
  id,
  gameId: null,
  rattachement: null,
  auteurNom: "Sam",
  auteurDiscordId: "111111111111111111",
  publieLe: "2026-10-16T22:30:00Z",
  nomFichier: "pentakill.mp4",
  contentType: "video/mp4",
  taille: 12_000_000,
  ...overrides,
});

const candidate = (gameId: number, gameNumber: number, lanceeLe: string): Schemas["GameCandidateDuClip"] => ({
  gameId,
  seriesId: 5,
  gameNumber,
  bestOf: 3,
  lanceeLe,
});

describe("Clips à rattacher", () => {
  it("liste les clips sans Game et en rattache un à une des Games proposées", async () => {
    let sansGame = [clip(7), clip(8, { nomFichier: "baron.webm", auteurNom: "Léa" })];
    const api = mockApi({
      "GET /auth/me": user(),
      "GET /clips/sans-game": () => json(sansGame),
      "GET /clips/7/games-candidates": [
        candidate(43, 2, "2026-10-16T20:40:00Z"),
        candidate(42, 1, "2026-10-16T20:00:00Z"),
      ],
      "GET /clips/8/games-candidates": [],
      "PUT /clips/7/game": async (request) => {
        const { gameId } = (await request.json()) as { gameId: number };
        sansGame = sansGame.filter((c) => c.id !== 7);
        return json(clip(7, { gameId, rattachement: "manuel" }));
      },
    });

    renderApp("/clips");

    expect(await screen.findByRole("heading", { name: "Clips à rattacher" })).toBeInTheDocument();
    const premier = await screen.findByRole("article", { name: "pentakill.mp4" });
    expect(premier.querySelector("video")).toHaveAttribute("src", "/api/clips/7/fichier");
    expect(within(premier).getByText(/Sam/)).toBeInTheDocument();
    const second = screen.getByRole("article", { name: "baron.webm" });
    expect(await within(second).findByText(/Aucune Game lancée/)).toBeInTheDocument();

    await userEvent.click(await within(premier).findByRole("button", { name: /Game 2 · BO3/ }));

    await vi.waitFor(() => expect(screen.queryByRole("article", { name: "pentakill.mp4" })).not.toBeInTheDocument());
    const put = api.mock.calls.map(([r]) => r).find((r) => r.method === "PUT");
    expect(put?.url).toMatch(/\/api\/clips\/7\/game$/);
  });

  it("rattache un clip à une Game donnée par son numéro", async () => {
    let sansGame = [clip(7)];
    const api = mockApi({
      "GET /auth/me": user(),
      "GET /clips/sans-game": () => json(sansGame),
      "GET /clips/7/games-candidates": [],
      "PUT /clips/7/game": () => {
        sansGame = [];
        return json(clip(7, { gameId: 12, rattachement: "manuel" }));
      },
    });

    renderApp("/clips");

    const premier = await screen.findByRole("article", { name: "pentakill.mp4" });
    await userEvent.type(within(premier).getByLabelText("N° de Game"), "12");
    await userEvent.click(within(premier).getByRole("button", { name: "Rattacher" }));

    expect(await screen.findByText(/Aucun clip à rattacher/)).toBeInTheDocument();
    const put = api.mock.calls.map(([r]) => r).find((r) => r.method === "PUT");
    expect(await put?.clone().json()).toEqual({ gameId: 12 });
  });

  it("est dans la navigation", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips/sans-game": [] });

    renderApp("/clips");

    expect(await screen.findByRole("link", { name: "Clips" })).toHaveAttribute("href", "/clips");
    expect(await screen.findByText(/Aucun clip à rattacher/)).toBeInTheDocument();
  });
});
