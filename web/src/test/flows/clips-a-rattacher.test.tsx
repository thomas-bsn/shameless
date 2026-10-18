import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Clip = Schemas["ClipDeLaListe"];

const game = (gameId: number, gameNumber: number, overrides: Partial<Schemas["GameDeLaListeDeClips"]> = {}): Schemas["GameDeLaListeDeClips"] => ({
  gameId,
  seriesId: 5,
  gameNumber,
  bestOf: 3,
  lanceeLe: "2026-10-17T12:00:00Z",
  scoreA: 1,
  scoreB: 0,
  ...overrides,
});

const clip = (id: number, overrides: Partial<Clip> = {}): Clip => ({
  id,
  gameId: null,
  rattachement: null,
  auteurNom: "Sam",
  auteurDiscordId: "111111111111111111",
  publieLe: "2026-10-17T13:30:00Z",
  nomFichier: "pentakill.mp4",
  contentType: "video/mp4",
  taille: 12_000_000,
  game: null,
  ...overrides,
});

const rattache = (id: number, g: Schemas["GameDeLaListeDeClips"], overrides: Partial<Clip> = {}) =>
  clip(id, { gameId: g.gameId, rattachement: "automatique", game: g, ...overrides });

const candidate = (gameId: number, gameNumber: number, lanceeLe: string): Schemas["GameCandidateDuClip"] => ({
  gameId,
  seriesId: 5,
  gameNumber,
  bestOf: 3,
  lanceeLe,
});

/** Répond à GET /clips selon le filtre sansGame, comme l'API. */
const liste = (clips: () => Clip[]) => (request: Request) => {
  const sansGame = new URL(request.url).searchParams.get("sansGame");
  const items = clips().filter((c) => (sansGame == null ? true : (c.gameId == null) === (sansGame === "true")));
  return json({ items, total: items.length, page: 1, taille: 50 });
};

describe("Onglet Clips", () => {
  it("range les clips par soirée puis par game avec son score, et met les clips sans game à rattacher en tête", async () => {
    const g1 = game(41, 1, { lanceeLe: "2026-10-16T12:00:00Z", scoreA: 0, scoreB: 1 });
    const g2 = game(42, 2);
    mockApi({
      "GET /auth/me": user(),
      "GET /clips": liste(() => [
        clip(7, { nomFichier: "orphelin.mp4", auteurNom: "Elio" }),
        rattache(1, g2, { nomFichier: "baron.mp4" }),
        rattache(2, g1, { nomFichier: "dragon.mp4" }),
      ]),
    });

    renderApp("/clips");

    const aRattacher = await screen.findByRole("region", { name: /À rattacher/ });
    expect(within(aRattacher).getByRole("button", { name: "Lire le clip orphelin.mp4" })).toBeInTheDocument();
    const soiree17 = await screen.findByRole("region", { name: /17 octobre/ });
    const game2 = within(soiree17).getByRole("group", { name: "Série 5 · Game 2" });
    expect(within(game2).getByText("1 — 0")).toBeInTheDocument();
    expect(within(game2).getByRole("button", { name: "Lire le clip baron.mp4" })).toBeInTheDocument();
    const soiree16 = screen.getByRole("region", { name: /16 octobre/ });
    const game1 = within(soiree16).getByRole("group", { name: "Série 5 · Game 1" });
    expect(within(game1).getByText("0 — 1")).toBeInTheDocument();
    expect(aRattacher.compareDocumentPosition(soiree17) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
  });

  it("lit un clip dans une fenêtre, avec précédent, suivant et l'ouverture de sa game", async () => {
    const g2 = game(42, 2);
    mockApi({
      "GET /auth/me": user(),
      "GET /clips": liste(() => [
        rattache(1, g2, { nomFichier: "premier.mp4", auteurNom: "Mina" }),
        rattache(2, g2, { nomFichier: "second.mp4", auteurNom: "Léa" }),
      ]),
    });

    renderApp("/clips");
    await userEvent.click(await screen.findByRole("button", { name: "Lire le clip premier.mp4" }));

    const lecteur = await screen.findByRole("dialog", { name: /^Clip de/ });
    expect(within(lecteur).getByText("Clip de Mina")).toBeInTheDocument();
    expect(lecteur.querySelector("video")).toHaveAttribute("src", "/api/clips/1/fichier");
    expect(within(lecteur).getByRole("link", { name: "Ouvrir la game" })).toHaveAttribute("href", "/games/42");

    await userEvent.click(within(lecteur).getByRole("button", { name: "Clip suivant" }));
    expect(within(lecteur).getByText("Clip de Léa")).toBeInTheDocument();
    expect(lecteur.querySelector("video")).toHaveAttribute("src", "/api/clips/2/fichier");
    expect(within(lecteur).getByRole("button", { name: "Clip suivant" })).toBeDisabled();

    await userEvent.click(within(lecteur).getByRole("button", { name: "Clip précédent" }));
    expect(within(lecteur).getByText("Clip de Mina")).toBeInTheDocument();
  });

  it("retire un clip de sa game depuis le lecteur : il passe dans À rattacher", async () => {
    let clips = [rattache(1, game(42, 2), { nomFichier: "baron.mp4" })];
    const api = mockApi({
      "GET /auth/me": user(),
      "GET /clips": liste(() => clips),
      "DELETE /clips/1/game": () => {
        clips = [clip(1, { nomFichier: "baron.mp4" })];
        return json(clips[0]);
      },
    });

    renderApp("/clips");
    await userEvent.click(await screen.findByRole("button", { name: "Lire le clip baron.mp4" }));
    await userEvent.click(await screen.findByRole("button", { name: /Retirer de la game/i }));

    await vi.waitFor(() => expect(api.mock.calls.some(([r]) => r.method === "DELETE")).toBe(true));
    expect(await screen.findByRole("region", { name: /À rattacher/ })).toBeInTheDocument();
  });

  it("change la game d'un clip depuis le lecteur", async () => {
    let clips = [rattache(1, game(42, 2), { nomFichier: "baron.mp4" })];
    const api = mockApi({
      "GET /auth/me": user(),
      "GET /clips": liste(() => clips),
      "GET /clips/1/games-candidates": [candidate(43, 3, "2026-10-17T13:00:00Z")],
      "PUT /clips/1/game": async (request) => {
        const { gameId } = (await request.clone().json()) as { gameId: number };
        clips = [rattache(1, game(gameId, 3), { nomFichier: "baron.mp4", rattachement: "manuel" })];
        return json(clips[0]);
      },
    });

    renderApp("/clips");
    await userEvent.click(await screen.findByRole("button", { name: "Lire le clip baron.mp4" }));
    await userEvent.click(await screen.findByRole("button", { name: "Changer de game" }));
    const fenetre = await screen.findByRole("dialog", { name: "À quelle game ce clip appartient-il ?" });
    await userEvent.click(await within(fenetre).findByRole("button", { name: /Game 3 · BO3/ }));

    await vi.waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    const put = api.mock.calls.map(([r]) => r).find((r) => r.method === "PUT");
    expect(await put?.clone().json()).toEqual({ gameId: 43 });
  });

  it("rattache un clip sans game à une des games proposées", async () => {
    let clips = [clip(7)];
    const api = mockApi({
      "GET /auth/me": user(),
      "GET /clips": liste(() => clips),
      "GET /clips/7/games-candidates": [
        candidate(43, 2, "2026-10-16T20:40:00Z"),
        candidate(42, 1, "2026-10-16T20:00:00Z"),
      ],
      "PUT /clips/7/game": async (request) => {
        const { gameId } = (await request.clone().json()) as { gameId: number };
        clips = [rattache(7, game(gameId, 2), { rattachement: "manuel" })];
        return json(clips[0]);
      },
    });

    renderApp("/clips");
    await userEvent.click(await screen.findByRole("button", { name: "Rattacher pentakill.mp4" }));
    const fenetre = await screen.findByRole("dialog", { name: "À quelle game ce clip appartient-il ?" });
    await userEvent.click(await within(fenetre).findByRole("button", { name: /Game 2 · BO3/ }));

    expect(await screen.findByText(/Tout est rattaché/)).toBeInTheDocument();
    const put = api.mock.calls.map(([r]) => r).find((r) => r.method === "PUT");
    expect(put?.url).toMatch(/\/api\/clips\/7\/game$/);
    expect(await put?.clone().json()).toEqual({ gameId: 43 });
  });

  it("rattache un clip à une game donnée par son numéro", async () => {
    let clips = [clip(7)];
    const api = mockApi({
      "GET /auth/me": user(),
      "GET /clips": liste(() => clips),
      "GET /clips/7/games-candidates": [],
      "PUT /clips/7/game": () => {
        clips = [rattache(7, game(12, 1), { rattachement: "manuel" })];
        return json(clips[0]);
      },
    });

    renderApp("/clips");
    await userEvent.click(await screen.findByRole("button", { name: "Rattacher pentakill.mp4" }));
    const fenetre = await screen.findByRole("dialog", { name: "À quelle game ce clip appartient-il ?" });
    expect(await within(fenetre).findByText(/Aucune Game lancée/)).toBeInTheDocument();
    await userEvent.type(within(fenetre).getByLabelText("N° de Game"), "12");
    await userEvent.click(within(fenetre).getByRole("button", { name: "Rattacher" }));

    await vi.waitFor(() => expect(screen.queryByRole("dialog")).not.toBeInTheDocument());
    const put = api.mock.calls.map(([r]) => r).find((r) => r.method === "PUT");
    expect(await put?.clone().json()).toEqual({ gameId: 12 });
  });

  it("dit quand il n'y a aucun clip, avec le mode d'emploi", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips": liste(() => []) });

    renderApp("/clips");

    expect(await screen.findByText("Aucun clip pour l'instant")).toBeInTheDocument();
    expect(screen.getByText(/#clips/)).toBeInTheDocument();
    expect(screen.queryByText(/Tout est rattaché/)).not.toBeInTheDocument();
  });

  it("dit quand tout est rattaché", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips": liste(() => [rattache(1, game(42, 2))]) });

    renderApp("/clips");

    expect(await screen.findByText("Tout est rattaché")).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: /À rattacher/ })).not.toBeInTheDocument();
    expect(screen.queryByText("Aucun clip pour l'instant")).not.toBeInTheDocument();
  });

  it("est dans la navigation", async () => {
    mockApi({ "GET /auth/me": user(), "GET /clips": liste(() => []) });

    renderApp("/clips");

    expect(await screen.findByRole("link", { name: "Clips" })).toHaveAttribute("href", "/clips");
  });
});
