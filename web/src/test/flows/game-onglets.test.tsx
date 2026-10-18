import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Stats = Schemas["StatsDuParticipant"];

const stats = (overrides: Partial<Stats> = {}): Stats => ({
  kills: 1,
  deaths: 1,
  assists: 1,
  minionsKilled: 100,
  neutralMinionsKilled: 0,
  damageToChampions: null,
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
  champLevel: 15,
  position: null,
  ...overrides,
});

const participant = (playerId: number, prenom: string, s: Stats | null): Schemas["GameHistoryParticipantDto"] => ({
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  rankTier: "GOLD",
  rankDivision: 2,
  cost: 3,
  championId: null,
  stats: s,
  performance: null,
});

/** Équipe A (rouge) : Alice 20 k + Chloé 10 k de dégâts ; équipe B (bleue) : Bob 15 k + Dan 5 k. Game de 30 min. */
const recap = (durationSeconds: number | null = 1800): Schemas["GameRecapResponse"] => ({
  id: 42,
  seriesId: null,
  gameNumber: 1,
  bestOf: 1,
  status: "completed",
  playedAt: "2026-10-16T20:30:00Z",
  completedAt: "2026-10-16T21:05:00Z",
  teamASide: "red",
  teamBSide: "blue",
  winner: "B",
  teamAWins: 0,
  teamBWins: 1,
  seriesOutcome: "Gagnee",
  riotMatchId: "EUW1_1",
  match: durationSeconds == null ? null : { durationSeconds, gameMode: "CLASSIC", gameType: "CUSTOM_GAME", gameVersion: null },
  teamA: [
    participant(1, "Alice", stats({ damageToChampions: 20000, damageTaken: 12000, goldEarned: 12000, turretKills: 2, dragonKills: 1, baronKills: 0, damageToObjectives: 8000, visionScore: 30, wardsPlaced: 10, wardsKilled: 4, controlWardsBought: 3 })),
    participant(3, "Chloé", stats({ damageToChampions: 10000, damageTaken: null, goldEarned: 8000, turretKills: 1, dragonKills: 2, baronKills: 1, damageToObjectives: 2000, visionScore: 60, wardsPlaced: 20, wardsKilled: 6, controlWardsBought: 5 })),
  ],
  teamB: [
    participant(2, "Bob", stats({ damageToChampions: 15000, damageTaken: 9000, goldEarned: 11000, turretKills: 4, dragonKills: 0, baronKills: 0, damageToObjectives: 6000, visionScore: 20, wardsPlaced: 7, wardsKilled: 2, controlWardsBought: 1 })),
    participant(4, "Dan", stats({ damageToChampions: 5000, damageTaken: 7000, goldEarned: 9000, turretKills: 0, dragonKills: 0, baronKills: 0, damageToObjectives: null, visionScore: 10, wardsPlaced: 3, wardsKilled: 1, controlWardsBought: 0 })),
  ],
});

const tuile = (nom: string) => screen.getByRole("group", { name: nom });
const ligne = (prenom: string) => screen.getByRole("row", { name: new RegExp(prenom) });

function cellule(row: HTMLElement, colonne: string) {
  const table = row.closest("table") as HTMLElement;
  const index = within(table)
    .getAllByRole("columnheader")
    .findIndex((h) => h.textContent === colonne);
  return within(row).getAllByRole("cell")[index];
}

async function ouvrir(onglet: string, game = recap()) {
  mockApi({ "GET /auth/me": user(), "GET /games/42": game });
  renderApp("/games/42");
  await userEvent.click(await screen.findByRole("tab", { name: onglet }));
}

describe("Onglet Dégâts", () => {
  it("affiche les tuiles de totaux par équipe, cohérents avec les lignes des joueurs", async () => {
    await ouvrir("Dégâts");

    // La tuile liste l'équipe bleue (B : 20 k) puis la rouge (A : 30 k).
    expect(tuile("Dégâts infligés")).toHaveTextContent("20,0 k");
    expect(tuile("Dégâts infligés")).toHaveTextContent("30,0 k");
    expect(tuile("Dégâts par minute")).toHaveTextContent("667");
    expect(tuile("Dégâts par minute")).toHaveTextContent("1 000");
    expect(tuile("Or gagné")).toHaveTextContent("20,0 k");
  });

  it("ajoute la tuile des dégâts subis et signale un total d'équipe partiel", async () => {
    await ouvrir("Dégâts");

    // Équipe bleue (B) : 9 + 7 = 16 k, complet. Équipe rouge (A) : seul Alice a la stat (12 k), Chloé non.
    const subis = tuile("Dégâts subis");
    expect(subis).toHaveTextContent("16,0 k");
    expect(subis).toHaveTextContent("≥ 12,0 k");
    expect(within(subis).getAllByTitle("Total partiel : 1 joueur sans cette stat")).toHaveLength(1);
    // Les totaux complets ne sont pas marqués.
    expect(within(tuile("Dégâts infligés")).queryByTitle(/partiel/)).not.toBeInTheDocument();
  });

  it("affiche par joueur les dégâts infligés, par minute, la part de l'équipe et les dégâts subis", async () => {
    await ouvrir("Dégâts");

    const alice = ligne("Alice");
    expect(cellule(alice, "Dégâts infligés")).toHaveTextContent("20,0 k");
    expect(cellule(alice, "Par minute")).toHaveTextContent("667 /min");
    expect(cellule(alice, "Part de l'équipe")).toHaveTextContent("67 %");
    expect(cellule(alice, "Dégâts subis")).toHaveTextContent("12,0 k");
    expect(cellule(ligne("Chloé"), "Dégâts subis")).toHaveTextContent("–");
  });

  it("n'affiche aucune valeur par minute quand la durée est inconnue", async () => {
    await ouvrir("Dégâts", recap(null));

    expect(cellule(ligne("Alice"), "Par minute")).toHaveTextContent("–");
    expect(cellule(ligne("Alice"), "Par minute")).not.toHaveTextContent("/min");
    expect(tuile("Dégâts par minute")).not.toHaveTextContent("/min");
    expect(tuile("Dégâts par minute")).not.toHaveTextContent(/\d/);
    expect(cellule(ligne("Alice"), "Part de l'équipe")).toHaveTextContent("67 %");
  });
});

describe("Onglet Objectifs", () => {
  it("affiche les totaux de tours, dragons, barons et les lignes par joueur", async () => {
    await ouvrir("Objectifs");

    expect(tuile("Tours")).toHaveTextContent("4");
    expect(tuile("Tours")).toHaveTextContent("3");
    expect(tuile("Dragons")).toHaveTextContent("3");
    expect(tuile("Barons")).toHaveTextContent("1");

    const chloe = ligne("Chloé");
    expect(cellule(chloe, "Tours")).toHaveTextContent("1");
    expect(cellule(chloe, "Dragons")).toHaveTextContent("2");
    expect(cellule(chloe, "Barons")).toHaveTextContent("1");
    expect(cellule(chloe, "Dégâts aux objectifs")).toHaveTextContent("2,0 k");
    expect(cellule(ligne("Dan"), "Dégâts aux objectifs")).toHaveTextContent("–");
  });
});

describe("Onglet Vision", () => {
  it("affiche le score de vision, les balises posées, détruites et de contrôle", async () => {
    await ouvrir("Vision");

    expect(tuile("Score de vision")).toHaveTextContent("30");
    expect(tuile("Score de vision")).toHaveTextContent("90");
    expect(tuile("Balises posées")).toHaveTextContent("30");
    expect(tuile("Balises détruites")).toHaveTextContent("10");
    expect(tuile("Balises de contrôle")).toHaveTextContent("1");
    expect(tuile("Balises de contrôle")).toHaveTextContent("8");

    const chloe = ligne("Chloé");
    expect(cellule(chloe, "Score de vision")).toHaveTextContent("60");
    expect(cellule(chloe, "Par minute")).toHaveTextContent("2,0 /min");
    expect(cellule(chloe, "Posées")).toHaveTextContent("20");
    expect(cellule(chloe, "Détruites")).toHaveTextContent("6");
    expect(cellule(chloe, "Contrôle")).toHaveTextContent("5");
  });
});

describe("Onglet Replay", () => {
  it("est grisé et explique pourquoi le replay 3D n'est pas disponible", async () => {
    await ouvrir("Replay");

    expect(screen.getByRole("tab", { name: "Replay" })).toHaveAttribute("data-state", "active");
    expect(screen.getByRole("heading", { name: "Le replay 3D arrive bientôt" })).toBeInTheDocument();
    expect(screen.getByText(/positions des joueurs/)).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });
});

describe("Game saisie à la main", () => {
  it("n'affiche pas de tableau de stats dans les onglets de stats, mais explique pourquoi", async () => {
    const base = recap();
    await ouvrir("Dégâts", {
      ...base,
      match: null,
      teamA: [{ ...base.teamA[0], stats: null }],
      teamB: [{ ...base.teamB[0], stats: null }],
    });

    expect(screen.getByText(/stats détaillées .* ne sont pas disponibles/)).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
  });
});
