import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { ORIGINE_VIDE, STATS_VIDES, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Impact = Schemas["ImpactDuJoueur"];
type Participant = Schemas["GameHistoryParticipantDto"];

const impactDAlice: Impact = {
  sousScores: [
    {
      groupe: "Global",
      score: 74,
      lettre: "A",
      points: 74.4,
      lignes: [
        { critere: "Kda", valeur: 5.25, score: 78, poids: 0.2, points: 15.6 },
        { critere: "OrParMinute", valeur: 412, score: 66, poids: 0.2, points: 13.2 },
        { critere: "PartDesDegats", valeur: 0.29, score: 61, poids: 0.1, points: 6.1 },
        { critere: "Victoire", valeur: 1, score: 100, poids: 0.1, points: 10 },
      ],
    },
    {
      groupe: "VsAdversaire",
      score: 70,
      lettre: "A",
      points: 28.8,
      lignes: [{ critere: "CsParMinute", valeur: 7.4, score: 72, poids: 0.2, points: 14.4 }],
    },
    {
      groupe: "Objectifs",
      score: 40,
      lettre: "C",
      points: 4,
      lignes: [{ critere: "ObjectifsParMinute", valeur: 350, score: 40, poids: 0.1, points: 4 }],
    },
    {
      groupe: "Equipe",
      score: 50,
      lettre: "C",
      points: 5,
      lignes: [{ critere: "ParticipationAuxKills", valeur: 0.65, score: 50, poids: 0.1, points: 5 }],
    },
    {
      groupe: "Role",
      score: 90,
      lettre: "S+",
      points: 9,
      lignes: [{ critere: "VisionParMinute", valeur: 0.9, score: 90, poids: 0.1, points: 9 }],
    },
  ],
  faceAFace: {
    adversaireId: 2,
    role: "Mid",
    axes: [
      { axe: "Survie", score: 72, scoreAdversaire: 55, ecart: 17 },
      { axe: "Utilite", score: 48, scoreAdversaire: 60, ecart: -12 },
    ],
    axesGagnes: 1,
    axesPerdus: 1,
  },
};

const participant = (playerId: number, prenom: string, overrides: Partial<Participant> = {}): Participant => ({
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  avatarUrl: null,
  rankTier: "GOLD",
  rankDivision: 2,
  cost: 3,
  championId: null,
  stats: { ...STATS_VIDES, kills: 7, deaths: 2, assists: 9, position: "Mid" },
  performance: { note: 74, role: "Mid", sourceDuRole: "Partie", composantes: [], noteOrigine: null },
  champsCorriges: [],
  details: null,
  place: null,
  badge: null,
  impact: null,
  ...ORIGINE_VIDE,
  ...overrides,
});

const recap = (equipeA: Participant[], equipeB: Participant[]): Schemas["GameRecapResponse"] => ({
  id: 42,
  seriesId: null,
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
  seriesOutcome: "Gagnee",
  riotMatchId: "EUW1_1",
  match: { durationSeconds: 1800, gameMode: "CLASSIC", gameType: "CUSTOM_GAME", gameVersion: null },
  teamAObjectifs: null,
  teamBObjectifs: null,
  chronologie: null,
  teamA: equipeA,
  teamB: equipeB,
});

const ligne = (prenom: string) => screen.getByRole("listitem", { name: prenom });

function ouvrirApp(game: Schemas["GameRecapResponse"]) {
  mockApi({ "GET /auth/me": user(), "GET /games/42": game });
  return renderApp("/games/42");
}

async function ouvrirImpactDAlice() {
  await screen.findByRole("list", { name: "Équipe A · Blue side" });
  await userEvent.click(within(ligne("Alice")).getByText("7"));
  return screen.findByRole("dialog");
}

const jeu = () =>
  recap(
    [participant(1, "Alice", { impact: impactDAlice })],
    [participant(2, "Bob", { stats: { ...STATS_VIDES, position: "Mid" } })],
  );

describe("Panneau Impact", () => {
  it("s'ouvre au clic sur la ligne du joueur sans quitter la page, avec son en-tête et sa note", async () => {
    const { router } = ouvrirApp(jeu());

    const panneau = await ouvrirImpactDAlice();

    expect(panneau).toHaveAccessibleName("Alice");
    expect(router.state.location.pathname).toBe("/games/42");
    expect(panneau).toHaveTextContent("Mid · Victoire · Équipe A");
    expect(within(panneau).getByRole("img", { name: "Note 74 sur 100" })).toBeInTheDocument();
  });

  it("ne mène au profil que par le pseudo, sans ouvrir l'Impact", async () => {
    const { router } = ouvrirApp(jeu());
    await screen.findByRole("list", { name: "Équipe A · Blue side" });

    await userEvent.click(within(ligne("Alice")).getByRole("link", { name: "Alice" }));

    expect(router.state.location.pathname).toBe("/joueurs/1");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("s'ouvre au clavier sur la ligne, se ferme par Échap et rend le focus à la ligne", async () => {
    ouvrirApp(jeu());
    await screen.findByRole("list", { name: "Équipe A · Blue side" });
    const alice = ligne("Alice");

    alice.focus();
    await userEvent.keyboard("{Enter}");
    expect(await screen.findByRole("dialog")).toBeInTheDocument();

    await userEvent.keyboard("{Escape}");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    await waitFor(() => expect(alice).toHaveFocus());
  });

  it("Entrée sur le pseudo suit le lien au lieu d'ouvrir l'Impact", async () => {
    const { router } = ouvrirApp(jeu());
    await screen.findByRole("list", { name: "Équipe A · Blue side" });

    within(ligne("Alice")).getByRole("link", { name: "Alice" }).focus();
    await userEvent.keyboard("{Enter}");

    expect(router.state.location.pathname).toBe("/joueurs/1");
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("montre dans l'onglet Global la lettre, les points et le tableau critère, valeur, score, points", async () => {
    ouvrirApp(jeu());
    const panneau = await ouvrirImpactDAlice();

    expect(within(panneau).getAllByRole("tab").map((t) => t.textContent)).toEqual([
      "Global",
      "Vs adversaire",
      "Objectifs",
      "Équipe",
      "Mid",
    ]);
    expect(within(panneau).getByRole("tab", { name: "Global" })).toHaveAttribute("aria-selected", "true");
    expect(within(panneau).getByText("+74,4 points")).toBeInTheDocument();
    expect(within(panneau).getByLabelText("Lettre A")).toBeInTheDocument();
    const tableau = within(panneau).getByRole("table");
    expect(within(tableau).getAllByRole("columnheader").map((c) => c.textContent)).toEqual([
      "Critère",
      "Valeur",
      "Score",
      "Points",
    ]);
    const kda = within(tableau).getByRole("row", { name: /KDA/ });
    expect(within(kda).getAllByRole("cell").map((c) => c.textContent)).toEqual(["KDA", "5,3", "78", "+15,6"]);
    const victoire = within(tableau).getByRole("row", { name: /Victoire/ });
    expect(within(victoire).getAllByRole("cell").map((c) => c.textContent)).toEqual(["Victoire", "oui", "100", "+10,0"]);
    expect(within(tableau).getByRole("row", { name: /dégâts de l'équipe/ })).toHaveTextContent("29 %");
  });

  it("change de groupe d'onglet en onglet, le rôle donnant le nom du dernier", async () => {
    ouvrirApp(jeu());
    const panneau = await ouvrirImpactDAlice();

    await userEvent.click(within(panneau).getByRole("tab", { name: "Vs adversaire" }));
    expect(within(panneau).getByRole("row", { name: /CS par minute/ })).toHaveTextContent("7,4");
    expect(within(panneau).queryByRole("row", { name: /KDA/ })).not.toBeInTheDocument();

    await userEvent.click(within(panneau).getByRole("tab", { name: "Mid" }));
    expect(within(panneau).getByRole("row", { name: /Vision par minute/ })).toHaveTextContent("0,9");
    expect(within(panneau).getByLabelText("Lettre S+")).toBeInTheDocument();
  });

  it("masque l'onglet d'un groupe absent", async () => {
    const sansRole: Impact = { ...impactDAlice, sousScores: impactDAlice.sousScores.filter((s) => s.groupe !== "Role") };
    ouvrirApp(recap([participant(1, "Alice", { impact: sansRole })], []));
    const panneau = await ouvrirImpactDAlice();

    expect(within(panneau).queryByRole("tab", { name: "Mid" })).not.toBeInTheDocument();
    expect(within(panneau).getAllByRole("tab")).toHaveLength(4);
  });

  it("montre le face-à-face avec l'adversaire, son score et ses écarts, axe par axe", async () => {
    ouvrirApp(jeu());
    await ouvrirImpactDAlice();

    const face = within(await screen.findByRole("region", { name: "Face-à-face" }));
    expect(face.getByText("Alice (Mid) contre Bob (Mid)")).toBeInTheDocument();
    expect(face.getByText("1 – 1")).toBeInTheDocument();
    const survie = face.getByRole("listitem", { name: "Survie" });
    expect(survie).toHaveTextContent("72 (+17)");
    expect(within(survie).getByRole("progressbar", { name: "Alice : 72" })).toHaveAttribute("value", "72");
    expect(within(survie).getByRole("progressbar", { name: "Bob : 55" })).toHaveAttribute("value", "55");
    expect(face.getByRole("listitem", { name: "Utilité" })).toHaveTextContent("48 (-12)");
    expect(face.queryByRole("listitem", { name: "Vision" })).not.toBeInTheDocument();
  });

  it("n'invente pas de face-à-face quand l'API n'en donne pas", async () => {
    const sans: Impact = { ...impactDAlice, faceAFace: null };
    ouvrirApp(recap([participant(1, "Alice", { impact: sans })], []));
    await ouvrirImpactDAlice();

    expect(screen.queryByRole("region", { name: "Face-à-face" })).not.toBeInTheDocument();
  });

  it("n'ouvre aucun panneau pour un joueur sans impact", async () => {
    const { router } = ouvrirApp(jeu());
    await screen.findByRole("list", { name: "Équipe B · Red side" });

    await userEvent.click(within(ligne("Bob")).getByText("Gold II"));

    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/games/42");
    expect(within(ligne("Bob")).getByRole("link", { name: "Bob" })).toHaveAttribute("href", "/joueurs/2");
  });
});
