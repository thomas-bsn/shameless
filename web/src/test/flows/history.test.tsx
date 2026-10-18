import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const team = (premierId: number, ...prenoms: string[]): Schemas["GameHistoryParticipantDto"][] =>
  prenoms.map((prenom, i) => ({
    prenom,
    riotId: `${prenom}#EUW`,
    rankTier: "GOLD",
    rankDivision: 2,
    cost: 3,
    playerId: premierId + i,
    championId: null,
    stats: null,
    performance: null,
  }));

const game = (id: number, winner: "A" | "B" | null, teamASide: "blue" | "red"): Schemas["GameHistoryResponse"] => ({
  id,
  seriesId: 5,
  playedAt: "2026-10-03T20:30:00Z",
  teamASide,
  teamBSide: teamASide === "blue" ? "red" : "blue",
  winner,
  riotMatchId: null,
  match: null,
  teamA: team(1, "Alice", "Bob"),
  teamB: team(3, "Chloé", "Dan"),
  mvp: [],
  dofd: [],
});

const october: Schemas["GameHistoryListResponse"] = {
  games: [
    {
      seriesId: 5,
      format: 3,
      playedAt: "2026-10-03T20:00:00Z",
      outcome: "Gagnee",
      winningTeam: "A",
      games: [game(11, "A", "blue"), game(12, "B", "red"), game(13, "A", "red")],
    },
    {
      seriesId: 6,
      format: 5,
      playedAt: "2026-10-03T22:00:00Z",
      outcome: "Interrompue",
      winningTeam: null,
      games: [{ ...game(14, "B", "blue"), seriesId: 6 }],
    },
  ],
};

afterEach(() => vi.useRealTimers());

describe("Historique", () => {
  it("shows the series of the month with their teams, score and winners", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/history/2026/10": october });

    renderApp("/historique?annee=2026&mois=10");

    expect(await screen.findByRole("heading", { name: "Octobre 2026" })).toBeInTheDocument();
    const series = await screen.findByRole("article", { name: "Série BO3" });
    expect(within(series).getByText("Équipe A 2 – 1 Équipe B")).toBeInTheDocument();
    expect(within(series).getByText("Gagnée par l'Équipe A")).toBeInTheDocument();
    const interrompue = screen.getByRole("article", { name: "Série BO5" });
    expect(within(interrompue).getByText("Interrompue")).toBeInTheDocument();

    const games = within(series).getAllByRole("listitem");
    expect(games).toHaveLength(3);
    expect(within(games[0]!).getByText("Équipe A gagne")).toBeInTheDocument();
    expect(within(games[1]!).getByText("Équipe B gagne")).toBeInTheDocument();
    expect(within(games[2]!).getByText("Équipe A gagne")).toBeInTheDocument();
    expect(within(games[1]!).getByRole("group", { name: "Équipe A · Red side" })).toHaveTextContent("Alice");
    expect(within(games[1]!).getByRole("link", { name: "Game 2" })).toHaveAttribute("href", "/games/12");
  });

  it("montre le MVP et le DOFD élus de chaque game", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/history/2026/10": {
        games: [
          {
            seriesId: 5,
            format: 1,
            playedAt: "2026-10-03T20:00:00Z",
            outcome: "Gagnee",
            winningTeam: "A",
            games: [{ ...game(11, "A", "blue"), mvp: [1], dofd: [4] }],
          },
        ],
      },
    });

    renderApp("/historique?annee=2026&mois=10");

    const equipeA = await screen.findByRole("group", { name: "Équipe A · Blue side" });
    expect(within(equipeA).getByText("Alice").parentElement).toHaveTextContent("MVP");
    expect(within(equipeA).queryByText("DOFD")).not.toBeInTheDocument();
    const equipeB = screen.getByRole("group", { name: "Équipe B · Red side" });
    expect(within(equipeB).getByText("Dan").parentElement).toHaveTextContent("DOFD");
    expect(within(equipeB).queryByText("MVP")).not.toBeInTheDocument();
  });

  it("navigates from month to month", async () => {
    const fetch = mockApi({
      "GET /auth/me": user(),
      "GET /games/history/2026/10": october,
      "GET /games/history/2026/9": { games: [] },
    });

    renderApp("/historique?annee=2026&mois=10");

    await userEvent.click(await screen.findByRole("link", { name: "Mois précédent" }));

    expect(await screen.findByRole("heading", { name: "Septembre 2026" })).toBeInTheDocument();
    expect(await screen.findByText("Aucune game ce mois-ci.")).toBeInTheDocument();
    expect(fetch.mock.calls.some(([r]) => r.url.endsWith("/api/games/history/2026/9"))).toBe(true);
    expect(screen.getByRole("link", { name: "Mois suivant" })).toBeInTheDocument();
  });

  it("stops at the current month", async () => {
    vi.useFakeTimers({ toFake: ["Date"] });
    vi.setSystemTime(new Date("2026-10-18T12:00:00"));
    mockApi({ "GET /auth/me": user(), "GET /games/history/2026/10": october });

    renderApp("/historique?annee=2026&mois=10");

    await screen.findByRole("heading", { name: "Octobre 2026" });
    expect(screen.queryByRole("link", { name: "Mois suivant" })).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Mois suivant" })).toBeDisabled();
    expect(screen.getByRole("link", { name: "Mois précédent" })).toBeInTheDocument();
  });

  it("marks the played days of the calendar with their number of games", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/history/2026/10": october });

    renderApp("/historique?annee=2026&mois=10");

    const calendrier = await screen.findByRole("group", { name: "Calendrier d'octobre 2026" });
    expect(within(calendrier).getByRole("button", { name: "3 octobre, 4 games" })).toHaveTextContent("4");
    expect(within(calendrier).queryByRole("button", { name: /^4 octobre/ })).not.toBeInTheDocument();
  });

  it("lists the series of the selected day with a link to each game", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/history/2026/10": {
        games: [
          { ...october.games[0]!, games: [game(11, "A", "blue")] },
          {
            seriesId: 0,
            format: 1,
            playedAt: "2026-10-10T20:00:00Z",
            outcome: null,
            winningTeam: null,
            games: [{ ...game(20, "B", "blue"), seriesId: 0, playedAt: "2026-10-10T20:00:00Z" }],
          },
        ],
      },
    });

    renderApp("/historique?annee=2026&mois=10");

    // Par défaut : le dernier jour joué (le 10, game hors série).
    expect(await screen.findByRole("article", { name: "Games hors série" })).toBeInTheDocument();
    expect(screen.queryByRole("article", { name: "Série BO3" })).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "3 octobre, 1 game" }));

    const serie = await screen.findByRole("article", { name: "Série BO3" });
    expect(within(serie).getByRole("link", { name: "Game 1" })).toHaveAttribute("href", "/games/11");
    expect(screen.queryByRole("article", { name: "Games hors série" })).not.toBeInTheDocument();
  });
});
