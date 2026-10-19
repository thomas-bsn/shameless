import { screen, within } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const recap = (overrides: Partial<Schemas["GameRecapResponse"]> = {}): Schemas["GameRecapResponse"] => ({
  id: 42,
  seriesId: 5,
  gameNumber: 1,
  bestOf: 1,
  status: "completed",
  playedAt: "2026-10-16T20:30:00Z",
  completedAt: "2026-10-16T21:05:00Z",
  modifieeParAdminLe: null,
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
  ...overrides,
});

const pronostics = (overrides: Partial<Schemas["PronosticsDeLaGame"]> = {}): Schemas["PronosticsDeLaGame"] => ({
  gameId: 42,
  gameNumber: 1,
  vainqueurConnu: true,
  votesClos: true,
  pronostics: [],
  ...overrides,
});

describe("Pronostics des Spectateurs", () => {
  it("montre dans le récap les choix justes et faux de chaque Spectateur, sans points ni classement", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap(),
      "GET /games/42/pronostics": pronostics({
        pronostics: [
          {
            pronostiqueurDiscordId: "900000000000000001",
            pronostiqueurNom: "Kiki",
            equipeGagnante: { equipe: "A", correct: true },
            mvp: { playerId: 3, prenom: "Alice", correct: true },
            dofd: { playerId: 4, prenom: "Dan", correct: false },
          },
          {
            pronostiqueurDiscordId: "900000000000000002",
            pronostiqueurNom: null,
            equipeGagnante: { equipe: "B", correct: false },
            mvp: null,
            dofd: null,
          },
        ],
      }),
    });

    renderApp("/games/42");

    const kiki = await screen.findByRole("group", { name: "Pronostic de Kiki" });
    expect(within(kiki).getByText("Vainqueur : Équipe A")).toBeInTheDocument();
    expect(within(kiki).getByText("MVP : Alice")).toBeInTheDocument();
    expect(within(kiki).getByText("DOFD : Dan")).toBeInTheDocument();
    expect(within(kiki).getAllByText("Juste")).toHaveLength(2);
    expect(within(kiki).getByText("Raté")).toBeInTheDocument();
    const anonyme = screen.getByRole("group", { name: "Pronostic de Spectateur 0002" });
    expect(within(anonyme).getByText("Raté")).toBeInTheDocument();
    expect(within(anonyme).queryByText(/MVP/)).not.toBeInTheDocument();
    expect(screen.queryByText(/point/i)).not.toBeInTheDocument();
  });

  it("garde en attente les choix dont le résultat n'est pas encore connu", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap(),
      "GET /games/42/pronostics": pronostics({
        votesClos: false,
        pronostics: [
          {
            pronostiqueurDiscordId: "900000000000000001",
            pronostiqueurNom: "Kiki",
            equipeGagnante: { equipe: "A", correct: true },
            mvp: { playerId: 3, prenom: "Alice", correct: null },
            dofd: null,
          },
        ],
      }),
    });

    renderApp("/games/42");

    const kiki = await screen.findByRole("group", { name: "Pronostic de Kiki" });
    expect(within(kiki).getByText("Juste")).toBeInTheDocument();
    expect(within(kiki).getByText("En attente")).toBeInTheDocument();
  });

  it("annonce l'absence de pronostic", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap(),
      "GET /games/42/pronostics": pronostics(),
    });

    renderApp("/games/42");

    expect(await screen.findByText("Aucun pronostic pour cette Game.")).toBeInTheDocument();
  });
});
