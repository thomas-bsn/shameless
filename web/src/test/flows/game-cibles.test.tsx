import { cleanup, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { ORIGINE_VIDE, STATS_VIDES, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Participant = Schemas["GameHistoryParticipantDto"];

const participant = (
  playerId: number,
  prenom: string,
  place: number | null,
  overrides: Partial<Participant> = {},
): Participant => ({
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  avatarUrl: null,
  rankTier: "GOLD",
  rankDivision: 2,
  cost: 3,
  championId: null,
  stats: { ...STATS_VIDES, kills: 1, deaths: 1, assists: 1, minionsKilled: 100, neutralMinionsKilled: 0, champLevel: 15, position: "Mid" },
  performance: place == null ? null : { note: 100 - place * 7, role: "Mid", sourceDuRole: "Partie", composantes: [], noteOrigine: null },
  champsCorriges: [],
  details: null,
  place,
  badge: null,
  ...ORIGINE_VIDE,
  ...overrides,
});

/** Équipe A (rouge, perdante) : places 1, 3, 5, 7, 9 ; équipe B (bleue, gagnante) : places 2, 4, 6, 8, 10. */
const NOMS = ["Alice", "Bob", "Chloé", "Dan", "Eve", "Fred", "Gina", "Hugo", "Iris", "Jules"];
const recap = (overrides: Partial<Schemas["GameRecapResponse"]> = {}): Schemas["GameRecapResponse"] => ({
  id: 42,
  seriesId: null,
  gameNumber: 1,
  bestOf: 1,
  status: "completed",
  playedAt: "2026-10-16T20:30:00Z",
  completedAt: "2026-10-16T21:05:00Z",
  modifieeParAdminLe: null,
  teamASide: "red",
  teamBSide: "blue",
  winner: "B",
  teamAWins: 0,
  teamBWins: 1,
  seriesOutcome: "Gagnee",
  riotMatchId: "EUW1_1",
  match: { durationSeconds: 1800, gameMode: "CLASSIC", gameType: "CUSTOM_GAME", gameVersion: null },
  teamAObjectifs: null,
  teamBObjectifs: null,
  teamA: [0, 2, 4, 6, 8].map((i) => participant(i + 1, NOMS[i]!, i + 1, i === 0 ? { badge: "Ace" } : {})),
  teamB: [1, 3, 5, 7, 9].map((i) => participant(i + 1, NOMS[i]!, i + 1, i === 1 ? { badge: "MeilleurePerf" } : {})),
  ...overrides,
});

const CATALOGUE = { version: "1", objets: [], sorts: [], arbres: [] };

async function ouvrirCibles(game = recap()) {
  mockApi({ "GET /auth/me": user(), "GET /games/42": game, "GET /catalogue": CATALOGUE });
  renderApp("/games/42");
  await userEvent.click(await screen.findByRole("tab", { name: "Performance" }));
}

const cible = () => screen.getByRole("group", { name: "Cibles" });

const anneau = (prenom: string) => {
  const lien = within(cible()).getByRole("link", { name: new RegExp(prenom) });
  return Number(lien.closest("[data-anneau]")!.getAttribute("data-anneau"));
};

describe("Vue en cibles", () => {
  beforeEach(() => localStorage.clear());

  it("met le meilleur de la game au centre et les moins bonnes notes vers l'extérieur", async () => {
    await ouvrirCibles();

    expect(anneau("Alice")).toBe(0);
    for (const prenom of ["Bob", "Chloé", "Dan"]) expect(anneau(prenom)).toBe(1);
    for (const prenom of ["Eve", "Fred", "Gina", "Hugo", "Iris", "Jules"]) expect(anneau(prenom)).toBe(2);
  });

  it("montre prénom, note, badges et couleur d'équipe de chaque joueur", async () => {
    await ouvrirCibles();

    const alice = within(cible()).getByRole("link", { name: /Alice/ });
    expect(alice).toHaveTextContent("93");
    expect(within(alice).getByText("ACE")).toBeInTheDocument();
    expect(alice).toHaveAttribute("data-equipe", "red");
    const bob = within(cible()).getByRole("link", { name: /Bob/ });
    expect(within(bob).getByText("MVP")).toBeInTheDocument();
    expect(bob).toHaveAttribute("data-equipe", "blue");
    const dan = within(cible()).getByRole("link", { name: /Dan/ });
    expect(within(dan).queryByText(/ACE|MVP/)).toBeNull();
  });

  it("n'affiche pas de légende sous la cible", async () => {
    await ouvrirCibles();

    expect(screen.queryByRole("group", { name: "Légende" })).toBeNull();
  });

  it("ouvre le profil du joueur au clic", async () => {
    await ouvrirCibles();

    expect(within(cible()).getByRole("link", { name: /Chloé/ })).toHaveAttribute("href", "/joueurs/3");
  });

  it("affiche un message plutôt qu'une cible vide pour une game sans notes", async () => {
    await ouvrirCibles(
      recap({
        teamA: [participant(1, "Alice", null), participant(3, "Chloé", null)],
        teamB: [participant(2, "Bob", null), participant(4, "Dan", null)],
      }),
    );

    expect(screen.getByText(/pas de notes/i)).toBeInTheDocument();
    expect(screen.queryByRole("group", { name: "Cibles" })).not.toBeInTheDocument();
  });

  it("conserve le choix de la vue d'une visite à l'autre", async () => {
    await ouvrirCibles();
    cleanup();

    renderApp("/games/42");
    expect(await screen.findByRole("tab", { name: "Performance" })).toHaveAttribute("data-state", "active");
    expect(screen.getByRole("tab", { name: "Général" })).toHaveAttribute("data-state", "inactive");
  });
});
