import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { shiftMonth } from "@/lib/history";
import { json, mockApi, problem } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Participant = Schemas["GameHistoryParticipantDto"];

const participant = (playerId: number, prenom: string, overrides: Partial<Participant> = {}): Participant => ({
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  avatarUrl: null,
  rankTier: "GOLD",
  rankDivision: 2,
  cost: 3,
  championId: "Ahri",
  stats: {
    kills: 5,
    deaths: 2,
    assists: 7,
    minionsKilled: 150,
    neutralMinionsKilled: 10,
    damageToChampions: 20000,
    damageTaken: 15000,
    goldEarned: 12000,
    visionScore: 30,
    wardsPlaced: 10,
    wardsKilled: 3,
    controlWardsBought: 2,
    damageToObjectives: 5000,
    turretKills: 1,
    dragonKills: 0,
    baronKills: 0,
    champLevel: 16,
    position: "Mid",
  },
  performance: { note: 70, role: "Mid", sourceDuRole: "Partie", composantes: [] },
  champsCorriges: [],
  ...overrides,
});

const NOMS_A = ["Alice", "Bob", "Chloé", "David", "Emma"];
const NOMS_B = ["Farid", "Gaëlle", "Hugo", "Inès", "Jules"];
const equipe = (debut: number, noms: string[]) => noms.map((n, i) => participant(debut + i, n));

const recap = (overrides: Partial<Schemas["GameRecapResponse"]> = {}): Schemas["GameRecapResponse"] => ({
  id: 42,
  seriesId: 7,
  gameNumber: 1,
  bestOf: 1,
  status: "Completed",
  playedAt: "2026-10-10T20:00:00Z",
  completedAt: "2026-10-10T20:40:00Z",
  modifieeParAdminLe: null,
  teamASide: "Blue",
  teamBSide: "Red",
  winner: "A",
  teamAWins: 1,
  teamBWins: 0,
  seriesOutcome: "Gagnee",
  riotMatchId: null,
  match: null,
  teamA: equipe(1, NOMS_A),
  teamB: equipe(11, NOMS_B),
  ...overrides,
});

const historique = (modifieeParAdminLe: string | null = null) => ({
  games: [
    {
      seriesId: 7,
      format: 1,
      playedAt: "2026-10-10T20:00:00Z",
      outcome: "Gagnee",
      winningTeam: "A",
      games: [
        {
          id: 42,
          seriesId: 7,
          playedAt: "2026-10-10T20:00:00Z",
          modifieeParAdminLe,
          teamASide: "Blue",
          teamBSide: "Red",
          winner: "A",
          riotMatchId: null,
          match: null,
          teamA: equipe(1, NOMS_A),
          teamB: equipe(11, NOMS_B),
          mvp: [],
          dofd: [],
        },
      ],
    },
  ],
});

const maintenant = new Date();
const mois = { year: maintenant.getFullYear(), month: maintenant.getMonth() + 1 };
const cle = (m: { year: number; month: number }) => `GET /games/history/${m.year}/${m.month}`;

const joueurs = [
  ...NOMS_A.map((prenom, i) => player({ id: 1 + i, prenom })),
  ...NOMS_B.map((prenom, i) => player({ id: 11 + i, prenom })),
  player({ id: 99, prenom: "Kevin" }),
];

const base = () => ({
  "GET /auth/me": user({ isAdmin: true }),
  "GET /games/pending": [],
  "GET /series/active": { series: [] },
  "GET /players": joueurs,
  [cle(mois)]: historique(),
  "GET /games/42": recap(),
});

const ouvrir = async () => {
  renderApp("/administration?onglet=games");
  await userEvent.click(await screen.findByRole("button", { name: "Ouvrir la game #42" }));
  return screen.findByRole("dialog");
};

const appel = (fetchMock: ReturnType<typeof mockApi>, methode: string, chemin: string) =>
  fetchMock.mock.calls.find(([r]) => r.method === methode && new URL(r.url).pathname === `/api${chemin}`);

const corps = async (fetchMock: ReturnType<typeof mockApi>, methode: string, chemin: string) => {
  await waitFor(() => expect(appel(fetchMock, methode, chemin)).toBeDefined());
  return appel(fetchMock, methode, chemin)![0].clone().text();
};

describe("Administration : onglet Games", () => {
  it("liste les games du mois, signale celles modifiées et navigue de mois en mois", async () => {
    mockApi({
      ...base(),
      [cle(mois)]: historique("2026-10-12T09:00:00Z"),
      [cle(shiftMonth(mois, -1))]: { games: [] },
    });
    renderApp("/administration?onglet=games");

    const ligne = (await screen.findByRole("button", { name: "Ouvrir la game #42" })).closest("tr") as HTMLElement;
    expect(within(ligne).getByText(/modifiée par un admin le 12\/10\/2026/)).toBeInTheDocument();
    expect(within(ligne).getByText(/Équipe A/)).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Mois précédent" }));
    expect(await screen.findByText("Aucune game terminée ce mois-ci.")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Mois suivant" }));
    expect(await screen.findByRole("button", { name: "Ouvrir la game #42" })).toBeInTheDocument();
  });

  it("corrige le vainqueur", async () => {
    const fetchMock = mockApi({ ...base(), "PUT /games/42/correction": recap({ winner: "B" }) });
    const dialogue = await ouvrir();

    await userEvent.click(within(dialogue).getByRole("button", { name: "Donner la victoire à l'Équipe B" }));

    expect(JSON.parse(await corps(fetchMock, "PUT", "/games/42/correction"))).toMatchObject({ winner: "B", teamA: null, teamB: null });
  });

  it("corrige la composition des équipes", async () => {
    const fetchMock = mockApi({ ...base(), "PUT /games/42/correction": recap() });
    const dialogue = await ouvrir();

    await userEvent.selectOptions(within(dialogue).getByLabelText("Équipe A, joueur 5"), "Kevin");
    await userEvent.click(within(dialogue).getByRole("button", { name: "Enregistrer la composition" }));

    expect(JSON.parse(await corps(fetchMock, "PUT", "/games/42/correction"))).toMatchObject({
      winner: null,
      teamA: [1, 2, 3, 4, 99],
      teamB: [11, 12, 13, 14, 15],
    });
  });

  it("refuse une composition avec un joueur en double sans appeler l'API", async () => {
    const fetchMock = mockApi({ ...base(), "PUT /games/42/correction": recap() });
    const dialogue = await ouvrir();

    await userEvent.selectOptions(within(dialogue).getByLabelText("Équipe A, joueur 5"), "Alice");
    await userEvent.click(within(dialogue).getByRole("button", { name: "Enregistrer la composition" }));

    expect(await within(dialogue).findByText(/5 joueurs distincts/)).toBeInTheDocument();
    expect(fetchMock.mock.calls.some(([r]) => r.method === "PUT")).toBe(false);
  });

  it("corrige un champ de stats, montre les champs corrigés et annule une correction", async () => {
    const fetchMock = mockApi({
      ...base(),
      "GET /games/42": recap({ teamA: [participant(1, "Alice", { champsCorriges: ["kills"] }), ...equipe(2, NOMS_A.slice(1))] }),
      "PUT /games/42/players/1/corrections": recap(),
      "DELETE /games/42/players/1/corrections/kills": recap(),
    });
    const dialogue = await ouvrir();

    expect(within(dialogue).getByText("Kills de Alice : corrigé")).toBeInTheDocument();
    const kills = within(dialogue).getByLabelText("Kills de Alice");
    await userEvent.clear(kills);
    await userEvent.type(kills, "12");
    await userEvent.click(within(dialogue).getByRole("button", { name: "Enregistrer les stats de Alice" }));
    expect(JSON.parse(await corps(fetchMock, "PUT", "/games/42/players/1/corrections"))).toMatchObject({ kills: 12, deaths: null, champion: null });

    await userEvent.click(within(dialogue).getByRole("button", { name: "Annuler la correction de Kills de Alice" }));
    await corps(fetchMock, "DELETE", "/games/42/players/1/corrections/kills");
  });

  it("affiche l'erreur de l'API quand une correction est refusée", async () => {
    mockApi({ ...base(), "PUT /games/42/correction": problem(409, "La série est terminée.", "Conflit") });
    const dialogue = await ouvrir();

    await userEvent.click(within(dialogue).getByRole("button", { name: "Donner la victoire à l'Équipe B" }));

    expect(await within(dialogue).findByText("La série est terminée.")).toBeInTheDocument();
  });

  it("supprime la game après une confirmation qui explique la conséquence", async () => {
    const fetchMock = mockApi({ ...base(), "DELETE /games/42": new Response(null, { status: 204 }) });
    const dialogue = await ouvrir();

    await userEvent.click(within(dialogue).getByRole("button", { name: "Supprimer la game" }));
    const confirmation = await screen.findByText(/série et le cost se recalculent/);
    expect(screen.getByText(/série vidée est effacée/)).toBeInTheDocument();
    expect(appel(fetchMock, "DELETE", "/games/42")).toBeUndefined();

    await userEvent.click(screen.getByRole("button", { name: "Supprimer définitivement" }));
    await corps(fetchMock, "DELETE", "/games/42");
    expect(confirmation).toBeDefined();
  });

  it("montre « modifiée par un admin » sur la page de la game", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap({ modifieeParAdminLe: "2026-10-12T09:00:00Z" }),
      "GET /games/42/votes": json({}),
    });
    renderApp("/games/42");

    expect(await screen.findByText(/modifiée par un admin le 12\/10\/2026/)).toBeInTheDocument();
  });
});
