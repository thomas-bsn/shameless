import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { json, mockApi, problem } from "@/test/api";
import { ORIGINE_VIDE, STATS_VIDES, player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

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
  stats: { ...STATS_VIDES, kills: 5, deaths: 2, assists: 7, minionsKilled: 150, neutralMinionsKilled: 10, position: "Mid", items: [3089, 0, 0, 0, 0, 0, 3340], perks: [8112, 8126, 8138, 8135, 8210, 8236] },
  performance: { note: 70, role: "Mid", sourceDuRole: "Partie", composantes: [], noteOrigine: null },
  champsCorriges: [],
  details: null,
  place: null,
  badge: null,
  ...ORIGINE_VIDE,
  ...overrides,
});

const NOMS_A = ["Alice", "Bob", "Chloé", "David", "Emma"];
const NOMS_B = ["Farid", "Gaëlle", "Hugo", "Inès", "Jules"];
const equipe = (debut: number, noms: string[]) => noms.map((n, i) => participant(debut + i, n));

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
  teamBObjectifs: null,
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
    { id: 8200, name: "Sorcellerie", iconUrl: "https://ddragon.test/perk/8200.png", runes: [{ id: 8210, name: "Transcendance", iconUrl: "https://ddragon.test/perk/8210.png" }] },
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
          mvp: [],
          dofd: [],
        },
      ],
    },
  ],
};

const maintenant = new Date();
const cleMois = `GET /games/history/${maintenant.getFullYear()}/${maintenant.getMonth() + 1}`;

export const base = (admin = true) => ({
  "GET /auth/me": user({ isAdmin: admin }),
  "GET /games/pending": [],
  "GET /series/active": { series: [] },
  "GET /players": joueurs,
  "GET /catalogue": catalogue,
  [cleMois]: historique,
  "GET /games/42": recap(),
});

const ouvrir = async () => {
  renderApp("/games/42");
  await userEvent.click(await screen.findByRole("button", { name: "Modifier la game" }));
  return screen.findByRole("dialog");
};

const appel = (fetchMock: ReturnType<typeof mockApi>, methode: string, chemin: string) =>
  fetchMock.mock.calls.find(([r]) => r.method === methode && new URL(r.url).pathname === `/api${chemin}`);

export const corps = async (fetchMock: ReturnType<typeof mockApi>, methode: string, chemin: string) => {
  await waitFor(() => expect(appel(fetchMock, methode, chemin)).toBeDefined());
  return appel(fetchMock, methode, chemin)![0].clone().text();
};

const onglet = (dialogue: HTMLElement, nom: string) => userEvent.click(within(dialogue).getByRole("tab", { name: nom }));

describe("Bouton « Modifier la game »", () => {
  it("est visible des Administrateurs sur la page de la game", async () => {
    mockApi(base(true));
    renderApp("/games/42");

    expect(await screen.findByRole("button", { name: "Modifier la game" })).toBeInTheDocument();
  });

  it("n'est pas visible des autres utilisateurs", async () => {
    mockApi(base(false));
    renderApp("/games/42");

    await screen.findByText(/Victoire de l'Équipe A/);
    expect(screen.queryByRole("button", { name: "Modifier la game" })).not.toBeInTheDocument();
  });
});

describe("Fenêtre Modifier la game : onglet Résultat", () => {
  it("montre l'effet d'un changement de vainqueur sur la Série, et le bouton est grisé sans changement", async () => {
    mockApi(base());
    const dialogue = await ouvrir();

    expect(within(dialogue).getByRole("heading", { name: "Modifier la game #42" })).toBeInTheDocument();
    const enregistrer = within(dialogue).getByRole("button", { name: "Enregistrer" });
    expect(enregistrer).toBeDisabled();

    await userEvent.click(within(dialogue).getByRole("radio", { name: /Équipe B/ }));

    expect(enregistrer).toBeEnabled();
    const effet = within(dialogue).getByText("Effet de ce changement").parentElement as HTMLElement;
    expect(effet).toHaveTextContent("Série BO3 2 – 1 → 1 – 2");
    expect(effet).toHaveTextContent("Vainqueur de la Série Équipe A → Équipe B");
    expect(effet).toHaveTextContent("Costs recalculés 10 joueurs");
  });

  it("enregistre le vainqueur et le confirme à côté du bouton", async () => {
    const fetchMock = mockApi({ ...base(), "PUT /games/42/correction": recap({ winner: "B" }) });
    const dialogue = await ouvrir();

    await userEvent.click(within(dialogue).getByRole("radio", { name: /Équipe B/ }));
    await userEvent.click(within(dialogue).getByRole("button", { name: "Enregistrer" }));

    expect(JSON.parse(await corps(fetchMock, "PUT", "/games/42/correction"))).toEqual({ winner: "B", teamA: null, teamB: null });
    const pied = within(dialogue).getByRole("button", { name: "Enregistrer" }).parentElement?.parentElement as HTMLElement;
    expect(await within(pied).findByText("Enregistré.")).toBeInTheDocument();
  });

  it("affiche le refus de l'API à côté du bouton, pas en haut de la fenêtre", async () => {
    mockApi({ ...base(), "PUT /games/42/correction": problem(409, "La série est terminée.", "Conflit") });
    const dialogue = await ouvrir();

    await userEvent.click(within(dialogue).getByRole("radio", { name: /Équipe B/ }));
    await userEvent.click(within(dialogue).getByRole("button", { name: "Enregistrer" }));

    const alerte = await within(dialogue).findByRole("alert");
    expect(alerte).toHaveTextContent("La série est terminée.");
    const bouton = within(dialogue).getByRole("button", { name: "Enregistrer" });
    expect(bouton.parentElement?.parentElement).toContainElement(alerte);
  });
});
