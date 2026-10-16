import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const team = (...prenoms: string[]): Schemas["GameHistoryParticipantDto"][] =>
  prenoms.map((prenom, i) => ({
    prenom,
    riotId: `${prenom}#EUW`,
    rankTier: "GOLD",
    rankDivision: 2,
    cost: 3,
    playerId: i + 1,
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
  teamA: team("Alice", "Bob"),
  teamB: team("Chloé", "Dan"),
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

  it("navigates from month to month", async () => {
    const fetch = mockApi({
      "GET /auth/me": user(),
      "GET /games/history/2026/10": october,
      "GET /games/history/2026/9": { games: [] },
    });

    renderApp("/historique?annee=2026&mois=10");

    await userEvent.click(await screen.findByRole("link", { name: "← Septembre 2026" }));

    expect(await screen.findByRole("heading", { name: "Septembre 2026" })).toBeInTheDocument();
    expect(await screen.findByText("Aucune game ce mois-ci.")).toBeInTheDocument();
    expect(fetch.mock.calls.some(([r]) => r.url.endsWith("/api/games/history/2026/9"))).toBe(true);
    expect(screen.getByRole("link", { name: "Octobre 2026 →" })).toBeInTheDocument();
  });
});
