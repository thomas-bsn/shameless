import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import type { mockApi } from "@/test/api";
import { ORIGINE_VIDE, STATS_VIDES, player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

/** Les données et gestes communs aux tests de flux de la fenêtre « Modifier la game ». */

type Participant = Schemas["GameHistoryParticipantDto"];

export const participant = (playerId: number, prenom: string, overrides: Partial<Participant> = {}): Participant => ({
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  avatarUrl: null,
  rankTier: "GOLD",
  rankDivision: 2,
  cost: 3,
  championId: "Ahri",
  stats: {
    ...STATS_VIDES,
    kills: 5,
    deaths: 2,
    assists: 7,
    minionsKilled: 150,
    neutralMinionsKilled: 10,
    position: "Mid",
    spell1Id: 4,
    spell2Id: 14,
    items: [3089, 0, 0, 0, 0, 0, 3340],
    perks: [8112, 8126, 8138, 8135, 8210, 8236],
  },
  performance: { note: 70, role: "Mid", sourceDuRole: "Partie", composantes: [], noteOrigine: null },
  champsCorriges: [],
  details: null,
  place: null,
  badge: null,
  impact: null,
  ...ORIGINE_VIDE,
  ...overrides,
});

export const NOMS_A = ["Alice", "Bob", "Chloé", "David", "Emma"];
export const NOMS_B = ["Farid", "Gaëlle", "Hugo", "Inès", "Jules"];
export const equipe = (debut: number, noms: string[]) => noms.map((n, i) => participant(debut + i, n));

export const recap = (overrides: Partial<Schemas["GameRecapResponse"]> = {}): Schemas["GameRecapResponse"] => ({
  id: 42,
  seriesId: 7,
  gameNumber: 3,
  bestOf: 3,
  status: "Completed",
  playedAt: "2026-10-10T20:00:00Z",
  completedAt: "2026-10-10T20:40:00Z",
  modifieeParAdminLe: null,
  teamASide: "Blue",
  teamBSide: "Red",
  winner: "A",
  teamAWins: 2,
  teamBWins: 1,
  seriesOutcome: "Gagnee",
  riotMatchId: null,
  match: null,
  teamAObjectifs: null,
  teamBObjectifs: null, chronologie: null,
  teamA: equipe(1, NOMS_A),
  teamB: equipe(11, NOMS_B),
  ...overrides,
});

const joueurs = [
  ...NOMS_A.map((prenom, i) => player({ id: 1 + i, prenom })),
  ...NOMS_B.map((prenom, i) => player({ id: 11 + i, prenom })),
  player({ id: 99, prenom: "Kevin" }),
];

export const catalogue: Schemas["CatalogueDeJeu"] = {
  version: "15.20.1",
  champions: [],
  objets: [
    { id: 3089, name: "Coiffe de Rabadon", iconUrl: "https://ddragon.test/item/3089.png" },
    { id: 3157, name: "Sablier de Zhonya", iconUrl: "https://ddragon.test/item/3157.png" },
    { id: 3340, name: "Totem de balise", iconUrl: "https://ddragon.test/item/3340.png" },
  ],
  sorts: [
    { id: 4, name: "Saut éclair", iconUrl: "https://ddragon.test/spell/SummonerFlash.png" },
    { id: 14, name: "Embrasement", iconUrl: "https://ddragon.test/spell/SummonerDot.png" },
  ],
  arbres: [
    {
      id: 8100,
      name: "Domination",
      iconUrl: "https://ddragon.test/perk/8100.png",
      runes: [
        { id: 8112, name: "Électrocution", iconUrl: "https://ddragon.test/perk/8112.png" },
        { id: 8126, name: "Coup bas", iconUrl: "https://ddragon.test/perk/8126.png" },
      ],
    },
    {
      id: 8200,
      name: "Sorcellerie",
      iconUrl: "https://ddragon.test/perk/8200.png",
      runes: [{ id: 8210, name: "Transcendance", iconUrl: "https://ddragon.test/perk/8210.png" }],
    },
  ],
};

const historique = {
  games: [
    {
      seriesId: 7,
      format: 3,
      playedAt: "2026-10-10T20:00:00Z",
      outcome: "Gagnee",
      winningTeam: "A",
      games: [
        {
          id: 42,
          seriesId: 7,
          playedAt: "2026-10-10T20:00:00Z",
          modifieeParAdminLe: null,
          teamASide: "Blue",
          teamBSide: "Red",
          winner: "A",
          riotMatchId: null,
          match: null,
          teamA: equipe(1, NOMS_A),
          teamB: equipe(11, NOMS_B),
          dofd: [],
        },
      ],
    },
  ],
};

const maintenant = new Date();
export const cleMois = `GET /games/history/${maintenant.getFullYear()}/${maintenant.getMonth() + 1}`;

export const base = (admin = true) => ({
  "GET /auth/me": user({ isAdmin: admin }),
  "GET /games/pending": [],
  "GET /series/active": { series: [] },
  "GET /players": joueurs,
  "GET /catalogue": catalogue,
  [cleMois]: historique,
  "GET /games/42": recap(),
});

/** Ouvre la fenêtre depuis la page de la game, comme un Administrateur. */
export const ouvrirAvecRouteur = async () => {
  const { router } = renderApp("/games/42");
  await userEvent.click(await screen.findByRole("button", { name: "Modifier la game" }));
  return { dialogue: await screen.findByRole("dialog"), router };
};

export const ouvrir = async () => (await ouvrirAvecRouteur()).dialogue;

const appel = (fetchMock: ReturnType<typeof mockApi>, methode: string, chemin: string) =>
  fetchMock.mock.calls.find(([r]) => r.method === methode && new URL(r.url).pathname === `/api${chemin}`);

export const appelA = (fetchMock: ReturnType<typeof mockApi>, methode: string, chemin: string) => appel(fetchMock, methode, chemin);

/** Le corps d'un appel, une fois qu'il a eu lieu. */
export const corps = async (fetchMock: ReturnType<typeof mockApi>, methode: string, chemin: string) => {
  await waitFor(() => expect(appel(fetchMock, methode, chemin)).toBeDefined());
  return appel(fetchMock, methode, chemin)![0].clone().text();
};

export const onglet = (dialogue: HTMLElement, nom: string) => userEvent.click(within(dialogue).getByRole("tab", { name: nom }));
