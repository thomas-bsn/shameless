import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { mockApi, problem } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const participant = (
  playerId: number,
  prenom: string,
  overrides: Partial<Schemas["GameHistoryParticipantDto"]> = {},
): Schemas["GameHistoryParticipantDto"] => ({
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  rankTier: "GOLD",
  rankDivision: 2,
  cost: 3,
  championId: null,
  stats: null,
  performance: null,
  ...overrides,
});

const stats = (kills: number, deaths: number, assists: number): Schemas["StatsDuParticipant"] => ({
  kills,
  deaths,
  assists,
  minionsKilled: 180,
  neutralMinionsKilled: 20,
  damageToChampions: 21000,
  damageTaken: null,
  goldEarned: null,
  visionScore: null,
  wardsPlaced: null,
  wardsKilled: null,
  controlWardsBought: null,
  damageToObjectives: null,
  turretKills: null,
  dragonKills: null,
  baronKills: null,
  champLevel: null,
  position: null,
});

const recap = (overrides: Partial<Schemas["GameRecapResponse"]> = {}): Schemas["GameRecapResponse"] => ({
  id: 42,
  seriesId: 5,
  gameNumber: 2,
  bestOf: 3,
  status: "completed",
  playedAt: "2026-10-16T20:30:00Z",
  completedAt: "2026-10-16T21:05:00Z",
  teamASide: "red",
  teamBSide: "blue",
  winner: "B",
  teamAWins: 1,
  teamBWins: 1,
  seriesOutcome: "EnCours",
  riotMatchId: "EUW1_1",
  match: { durationSeconds: 1834, gameMode: "CLASSIC", gameType: "CUSTOM_GAME", gameVersion: null },
  teamA: [participant(1, "Alice", { championId: "Ahri", stats: stats(7, 2, 9) })],
  teamB: [participant(2, "Bob")],
  ...overrides,
});

describe("Page d'une Game", () => {
  it("montre la Game dans sa Série, son vainqueur et les stats de chaque joueur", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/42": recap() });

    renderApp("/games/42");

    expect(await screen.findByRole("heading", { name: "Game 2 · Série BO3" })).toBeInTheDocument();
    expect(screen.getByText("Équipe B gagne")).toBeInTheDocument();
    expect(screen.getByText("Équipe A 1 – 1 Équipe B")).toBeInTheDocument();
    expect(screen.getByText("30:34")).toBeInTheDocument();
    const equipeA = screen.getByRole("group", { name: "Équipe A · Red side" });
    expect(within(equipeA).getByText("Alice")).toBeInTheDocument();
    expect(within(equipeA).getByText("Ahri")).toBeInTheDocument();
    expect(within(equipeA).getByText("7/2/9")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "Voir la Série" })).toHaveAttribute("href", "/series/5");
  });

  it("montre une Game en attente sans vainqueur", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap({ status: "pending", winner: null, completedAt: null, match: null }),
    });

    renderApp("/games/42");

    expect(await screen.findByText("En cours")).toBeInTheDocument();
    expect(screen.queryByText(/gagne/)).not.toBeInTheDocument();
  });

  it("signale une Game inconnue", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/42": problem(404, "Cette game n'existe pas.") });

    renderApp("/games/42");

    expect(await screen.findByText("Cette game n'existe pas.")).toBeInTheDocument();
  });

  it("affiche les métriques de dégâts par joueur et grise le replay avec sa raison", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/42": recap({ teamA: [participant(1, "Alice", { stats: stats(7, 2, 9) })] }) });
    renderApp("/games/42");
    await screen.findByRole("heading", { name: "Game 2 · Série BO3" });
    await userEvent.click(screen.getByRole("button", { name: "Dégâts" }));
    expect(await screen.findByLabelText("Dégâts par minute Alice")).toBeInTheDocument();
    expect(screen.getByLabelText("Part des dégâts de l'équipe Alice")).toBeInTheDocument();
    expect(screen.getAllByText("Dégâts subis")).toHaveLength(2);
    expect(screen.getByRole("button", { name: /Replay/ })).toBeDisabled();
    expect(screen.getByRole("button", { name: /positions absentes/ })).toBeInTheDocument();
  });

  it("entoure les joueurs MVP et DOFD élus", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap({ teamA: [participant(1, "Alice")], teamB: [participant(2, "Bob")] }),
      "GET /games/42/votes": { gameId: 42, statut: "clos", ouvertureLe: null, fermetureLe: null, votants: 2, mvp: { gagnants: [1], decompte: [{ playerId: 1, prenom: "Alice", voix: 2 }] }, dofd: { gagnants: [2], decompte: [{ playerId: 2, prenom: "Bob", voix: 2 }] } },
    });
    renderApp("/games/42");
    expect(await screen.findByLabelText("MVP Alice")).toBeInTheDocument();
    expect(screen.getByLabelText("DOFD Bob")).toBeInTheDocument();
  });

  it("montre les totaux Objectifs et Vision, et laisse le par minute absent sans durée", async () => {
    const aStats = { ...stats(5, 3, 8), turretKills: 2, dragonKills: 1, baronKills: 1, damageToObjectives: 4200, visionScore: 36, wardsPlaced: 15, wardsKilled: 4, controlWardsBought: 5 };
    mockApi({ "GET /auth/me": user(), "GET /games/42": recap({ match: null, teamA: [participant(1, "Alice", { stats: aStats })], teamB: [] }) });
    renderApp("/games/42");
    await screen.findByRole("heading", { name: "Game 2 · Série BO3" });
    await userEvent.click(screen.getByRole("button", { name: "Dégâts" }));
    expect(await screen.findByLabelText("Dégâts par minute Alice")).toHaveTextContent("—");
    await userEvent.click(screen.getByRole("button", { name: "Objectifs" }));
    expect(await screen.findAllByText("Tours")).toHaveLength(2);
    expect(screen.getAllByText("Dragons")).toHaveLength(2);
    expect(screen.getAllByText("Barons")).toHaveLength(2);
    await userEvent.click(screen.getByRole("button", { name: "Vision" }));
    expect(await screen.findAllByText("Balises de contrôle")).toHaveLength(2);
  });
});
