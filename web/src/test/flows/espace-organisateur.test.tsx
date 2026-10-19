import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi, problem } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Participant = Schemas["GameParticipantDto"];
type Series = Schemas["SeriesResponse"];

const participant = (playerId: number, prenom: string): Participant => ({
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  rankTier: null,
  rankDivision: null,
  cost: 3,
  role: null,
});

const game = (id: number, overrides: Partial<Schemas["SeriesGameResponse"]> = {}): Schemas["SeriesGameResponse"] => ({
  id,
  status: "completed",
  teamASide: "Blue",
  teamBSide: "Red",
  winner: "A",
  teamA: [],
  teamB: [],
  riotMatchId: null,
  championsJoues: [],
  rapportEnRevue: null,
  startedAt: null,
  ...overrides,
});

const attente = (id: number, overrides: Partial<Schemas["SeriesGameResponse"]> = {}) =>
  game(id, { status: "pending", winner: null, teamASide: "Red", teamBSide: "Blue", ...overrides });

const serie = (overrides: Partial<Series> = {}): Series => ({
  seriesId: 5,
  soireeId: 7,
  format: 3,
  fearless: false,
  outcome: "EnCours",
  winningTeam: null,
  teamAWins: 1,
  teamBWins: 0,
  teamA: [participant(1, "Aldric"), participant(2, "Kuro")],
  teamB: [participant(3, "Mina"), participant(4, "Elio")],
  capitaineA: null,
  capitaineB: null,
  games: [game(21)],
  ...overrides,
});

const routes = (series: Series = serie()) => ({
  "GET /auth/me": user(),
  "GET /series/5": series,
  "GET /players": [],
});

const appels = (fetch: ReturnType<typeof mockApi>, methode: string, fin: string) =>
  fetch.mock.calls.filter(([r]) => r.method === methode && r.url.endsWith(fin));

// jsdom n'implémente pas ces API, que le menu déroulant de Radix appelle.
beforeEach(() => {
  Element.prototype.hasPointerCapture ??= () => false;
  Element.prototype.setPointerCapture ??= () => {};
  Element.prototype.releasePointerCapture ??= () => {};
  Element.prototype.scrollIntoView ??= () => {};
  // Ni ResizeObserver, que les cases à cocher de Radix demandent dans un formulaire.
  vi.stubGlobal(
    "ResizeObserver",
    class {
      observe() {}
      unobserve() {}
      disconnect() {}
    },
  );
});

describe("Espace organisateur", () => {
  it("est réservé à l'organisateur connecté", async () => {
    mockApi({ "GET /auth/me": json({ status: 401 }, 401) });
    const { router } = renderApp("/series/5/organisateur");

    await vi.waitFor(() => expect(router.state.location.pathname).toBe("/connexion"));
  });

  it("lance la game suivante sans demander de Side, une fois la précédente terminée", async () => {
    const fetch = mockApi({ ...routes(), "POST /series/5/games": json({}, 201) });
    renderApp("/series/5/organisateur");

    const lancer = await screen.findByRole("button", { name: "Lancer la game 2" });
    expect(screen.queryByLabelText("Side de l'Équipe A")).not.toBeInTheDocument();
    await userEvent.click(lancer);

    await waitFor(() => expect(appels(fetch, "POST", "/api/series/5/games")).toHaveLength(1));
    expect(await appels(fetch, "POST", "/api/series/5/games")[0]![0].clone().text()).toBe("");
  });

  it("explique un refus 409 du lancement (capitaines pas prêts) et renvoie vers « Liens et code »", async () => {
    mockApi({
      ...routes(),
      "POST /series/5/games": problem(409, "Les deux Capitaines doivent avoir rejoint avant de lancer la draft."),
    });
    renderApp("/series/5/organisateur");

    await userEvent.click(await screen.findByRole("button", { name: "Lancer la game 2" }));

    const alerte = await screen.findByRole("alert");
    expect(alerte).toHaveTextContent("Les capitaines ne sont pas prêts");
    expect(alerte).not.toHaveTextContent("doivent avoir rejoint avant de lancer la draft");
    const liens = within(alerte).getByRole("link", { name: "Liens et code" });
    expect(liens).toHaveAttribute("href", "/series/5/preparation");
  });

  it("renvoie vers l'Écran de préparation tant qu'aucune game n'existe, sans bouton de lancement", async () => {
    mockApi(routes(serie({ games: [], teamAWins: 0 })));
    renderApp("/series/5/organisateur");

    await screen.findByRole("heading", { name: /Pilotage/ });
    expect(screen.queryByRole("button", { name: /Lancer la game/ })).not.toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Liens et code" })).toHaveAttribute("href", "/series/5/preparation");
  });

  it("montre la game en cours avec ses Sides et la saisie manuelle de secours", async () => {
    const fetch = mockApi({
      ...routes(serie({ games: [game(21), attente(22)] })),
      "PUT /games/22/result": json({ gameId: 22, winner: "B", teamASide: "Red", teamBSide: "Blue", status: "completed" }),
    });
    renderApp("/series/5/organisateur");

    expect(await screen.findByRole("region", { name: "Game 2 en cours" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Lancer la game/ })).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Victoire Équipe B" }));

    await waitFor(() => expect(appels(fetch, "PUT", "/api/games/22/result")).toHaveLength(1));
    expect(await appels(fetch, "PUT", "/api/games/22/result")[0]![0].clone().json()).toEqual({ winner: "B" });
  });

  it("signale à la main le début de la partie quand le Companion ne l'a pas fait", async () => {
    const fetch = mockApi({
      ...routes(serie({ games: [game(21), attente(22)] })),
      "POST /series/5/debut": json({ seriesId: 5, gameId: 22, startedAt: "2026-10-19T20:00:00" }),
    });
    renderApp("/series/5/organisateur");

    expect(await screen.findByText(/partie n'est pas encore lancée/)).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Signaler le début de la partie" }));

    await waitFor(() => expect(appels(fetch, "POST", "/api/series/5/debut")).toHaveLength(1));
  });

  it("n'offre plus le signal manuel une fois la partie commencée", async () => {
    mockApi(routes(serie({ games: [game(21), attente(22, { startedAt: "2026-10-19T20:00:00" })] })));
    renderApp("/series/5/organisateur");

    expect(await screen.findByRole("region", { name: "Game 2 en cours" })).toBeInTheDocument();
    expect(screen.getByText(/Partie commencée/)).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Signaler le début de la partie" })).not.toBeInTheDocument();
  });

  it("montre le Rapport en revue, avec le joueur inconnu, et bloque la saisie manuelle", async () => {
    mockApi({
      ...routes(
        serie({
          games: [
            game(21),
            attente(22, {
              rapportEnRevue: {
                id: 12,
                riotMatchId: "EUW1_1",
                recuLe: "2026-10-16T20:00:00",
                equipeGagnante: "B",
                absents: [],
                inattendus: [{ puuid: "p-inconnu", riotId: "Inconnu#EUW", side: "red", championId: "Ahri", joueur: null }],
                mauvaisSide: [],
              },
            }),
          ],
        }),
      ),
      "GET /champions": [],
    });
    renderApp("/series/5/organisateur");

    expect(await screen.findByRole("region", { name: "Rapport de la game 2 en revue" })).toBeInTheDocument();
    expect(screen.getByText("Inconnu#EUW")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Victoire Équipe A" })).toBeDisabled();
  });

  it("remplace un joueur entre deux games", async () => {
    const fetch = mockApi({
      ...routes(),
      "GET /players": [player({ id: 1, prenom: "Aldric" }), player({ id: 9, prenom: "Zoé" })],
      "POST /series/5/replacements": json(serie()),
    });
    renderApp("/series/5/organisateur");

    expect(await screen.findByRole("region", { name: "Remplacement" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("combobox", { name: "Joueur qui sort" }));
    await userEvent.click(await screen.findByRole("option", { name: "Aldric" }));
    await userEvent.click(screen.getByRole("combobox", { name: "Joueur qui entre" }));
    await userEvent.click(await screen.findByRole("option", { name: "Zoé" }));
    await userEvent.click(screen.getByRole("button", { name: "Remplacer" }));

    await waitFor(() => expect(appels(fetch, "POST", "/api/series/5/replacements")).toHaveLength(1));
    expect(await appels(fetch, "POST", "/api/series/5/replacements")[0]![0].clone().json()).toEqual({
      replacedPlayerId: 1,
      replacementPlayerId: 9,
    });
  });

  it("arrête la série après confirmation", async () => {
    vi.spyOn(window, "confirm").mockReturnValue(true);
    const fetch = mockApi({
      ...routes(),
      "POST /series/5/stop": json({ arret: "Interrompue" }),
    });
    renderApp("/series/5/organisateur");

    await userEvent.click(await screen.findByRole("button", { name: "Arrêter la série" }));

    await waitFor(() => expect(appels(fetch, "POST", "/api/series/5/stop")).toHaveLength(1));
  });

  it("rouvre l'Écran de préparation par « Liens et code »", async () => {
    mockApi({ ...routes(), "GET /series/5/liens": {}, "GET /series/5/code-companion": {} });
    const { router } = renderApp("/series/5/organisateur");

    await userEvent.click(await screen.findByRole("link", { name: "Liens et code" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/series/5/preparation"));
  });

  it("ouvre l'Écran de préparation de la nouvelle Série après une Revanche", async () => {
    mockApi({
      ...routes(serie({ outcome: "Gagnee", winningTeam: "A", teamAWins: 2, games: [game(21), game(22)] })),
      "POST /series/5/rematch": json({ seriesId: 9 }, 201),
    });
    const { router } = renderApp("/series/5/organisateur");

    await userEvent.click(await screen.findByRole("button", { name: "Lancer la revanche" }));

    await waitFor(() => expect(router.state.location.pathname).toBe("/series/9/preparation"));
  });

  it("propose la Revanche à la fin, sans les commandes de la série en cours", async () => {
    mockApi(routes(serie({ outcome: "Gagnee", winningTeam: "A", teamAWins: 2, games: [game(21), game(22)] })));
    renderApp("/series/5/organisateur");

    const revanche = await screen.findByRole("region", { name: "Revanche" });
    expect(within(revanche).getByRole("button", { name: "Lancer la revanche" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Lancer la game/ })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Arrêter la série" })).not.toBeInTheDocument();
    expect(screen.queryByRole("link", { name: "Liens et code" })).not.toBeInTheDocument();
  });
});
