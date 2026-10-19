import { screen } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const partieTerminee: Schemas["SeriesGameResponse"] = {
  id: 90,
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
};

const serie = (overrides: Partial<Schemas["SeriesResponse"]> = {}): Schemas["SeriesResponse"] => ({
  seriesId: 11,
  soireeId: 7,
  format: 3,
  fearless: false,
  outcome: "EnCours",
  winningTeam: null,
  teamAWins: 1,
  teamBWins: 0,
  teamA: [],
  teamB: [],
  capitaineA: null,
  capitaineB: null,
  games: [partieTerminee],
  ...overrides,
});

describe("Side automatique", () => {
  it("ne demande aucun Side et ne propose aucun lancement manuel", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /series/11": serie(),
    });
    renderApp("/series/11/organisateur");

    await screen.findByRole("region", { name: "Game 2" });
    expect(screen.queryByRole("button", { name: /Lancer la game/ })).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Side de l'Équipe A")).not.toBeInTheDocument();
    expect(screen.queryByText("Alternance automatique")).not.toBeInTheDocument();
  });

  it("n'affiche plus d'option de Sides alternés sur la série", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /series/11": serie(),
    });
    renderApp("/series/11/organisateur");

    await screen.findByRole("region", { name: "Game 2" });
    expect(screen.queryByText("Sides alternés")).not.toBeInTheDocument();
  });
});
