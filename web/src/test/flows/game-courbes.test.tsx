import { act, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { ORIGINE_VIDE, STATS_VIDES, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Chronologie = Schemas["ChronologieDeGameResponse"];
type JoueurDuPoint = Schemas["JoueurDuPointDto"];

const participant = (playerId: number, prenom: string, championId: string): Schemas["GameHistoryParticipantDto"] => ({
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  avatarUrl: null,
  rankTier: "GOLD",
  rankDivision: 2,
  cost: 3,
  championId,
  stats: { ...STATS_VIDES, kills: 1, deaths: 1, assists: 1 },
  performance: null,
  champsCorriges: [], details: null, place: null, badge: null, impact: null,
  ...ORIGINE_VIDE,
});

/** L'équipe A est rouge (Alice, Chloé), l'équipe B est bleue (Bob, Dan). */
const recap = (chronologie: Chronologie | null): Schemas["GameRecapResponse"] => ({
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
  match: { durationSeconds: 1200, gameMode: "CLASSIC", gameType: "CUSTOM_GAME", gameVersion: null },
  teamAObjectifs: null,
  teamBObjectifs: null,
  chronologie,
  teamA: [participant(1, "Alice", "Ahri"), participant(3, "Chloé", "Lux")],
  teamB: [participant(2, "Bob", "Zed"), participant(4, "Dan", "Jinx")],
});

const joueur = (playerId: number, surcharge: Partial<JoueurDuPoint>): JoueurDuPoint => ({
  playerId,
  kills: 0,
  deaths: 0,
  assists: 0,
  cs: 0,
  niveau: 1,
  vision: 0,
  valeurObjets: 0,
  mort: false,
  orEstime: 0,
  ...surcharge,
});

const SERIES: Chronologie["series"] = [
  { nom: "kills", nature: "reelle" },
  { nom: "morts", nature: "reelle" },
  { nom: "assists", nature: "reelle" },
  { nom: "cs", nature: "reelle" },
  { nom: "niveau", nature: "reelle" },
  { nom: "vision", nature: "reelle" },
  { nom: "objectifs", nature: "reelle" },
  { nom: "or", nature: "estimee" },
  { nom: "degats", nature: "indisponible" },
  { nom: "xp", nature: "indisponible" },
];

/** Trois points (0, 10 et 20 min) : l'équipe bleue (Bob, Dan) finit devant la rouge. */
const chronologie = (surcharge: Partial<Chronologie> = {}): Chronologie => ({
  series: SERIES,
  points: [
    {
      t: 0,
      joueurs: [joueur(1, {}), joueur(3, {}), joueur(2, {}), joueur(4, {})],
    },
    {
      t: 600,
      joueurs: [
        joueur(1, { kills: 1, cs: 50, niveau: 6, orEstime: 3000 }),
        joueur(3, { kills: 0, cs: 40, niveau: 5, orEstime: 2000 }),
        joueur(2, { kills: 2, cs: 70, niveau: 7, orEstime: 3500 }),
        joueur(4, { kills: 1, cs: 60, niveau: 6, orEstime: 3000 }),
      ],
    },
    {
      t: 1200,
      joueurs: [
        joueur(1, { kills: 3, cs: 120, niveau: 12, orEstime: 6000 }),
        joueur(3, { kills: 1, cs: 100, niveau: 11, orEstime: 4000 }),
        joueur(2, { kills: 5, cs: 150, niveau: 13, orEstime: 7000 }),
        joueur(4, { kills: 3, cs: 130, niveau: 12, orEstime: 5000 }),
      ],
    },
  ],
  evenements: [
    { t: 300, type: "dragon", side: "blue" },
    { t: 900, type: "turret", side: "red" },
  ],
  ...surcharge,
});

async function ouvrirCourbes(game: Schemas["GameRecapResponse"]) {
  mockApi({ "GET /auth/me": user(), "GET /games/42": game });
  renderApp("/games/42");
  await userEvent.click(await screen.findByRole("tab", { name: "Courbes" }));
}

const graphe = (nom: RegExp | string) => screen.getByRole("img", { name: nom });
const tuile = (nom: string) => screen.getByRole("group", { name: nom });

describe("Onglet Courbes", () => {
  it("se place entre Performance et Détails, avec une icône", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/42": recap(chronologie()) });
    renderApp("/games/42");

    await screen.findByRole("tab", { name: "Général" });
    const noms = screen.getAllByRole("tab").map((t) => t.textContent);
    expect(noms.indexOf("Courbes")).toBe(noms.indexOf("Performance") + 1);
    expect(noms.indexOf("Détails")).toBe(noms.indexOf("Courbes") + 1);
    expect(screen.getByRole("tab", { name: "Courbes" }).querySelector("svg[aria-hidden='true']")).not.toBeNull();
  });

  it("explique qu'une game sans chronologie n'a pas de courbes", async () => {
    await ouvrirCourbes(recap(null));

    expect(screen.getByText(/n'a pas de chronologie/)).toBeInTheDocument();
    expect(screen.queryByRole("img", { name: /Avantage/ })).not.toBeInTheDocument();
  });

  it("trace l'avantage d'or de l'équipe bleue, signalé estimé, avec sa valeur finale", async () => {
    await ouvrirCourbes(recap(chronologie()));

    expect(screen.getByRole("heading", { name: "Avantage d'or" })).toBeInTheDocument();
    // Bleu (Bob + Dan) 12 000 contre rouge (Alice + Chloé) 10 000.
    expect(graphe(/Avantage d'or/)).toHaveAccessibleName(/\+2\s?000/);
    expect(screen.getByRole("button", { name: /Or, estimé/ })).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("button", { name: /CS, réel/ })).toHaveAttribute("aria-pressed", "false");
  });

  it("grise les métriques indisponibles", async () => {
    await ouvrirCourbes(recap(chronologie()));

    expect(screen.getByRole("button", { name: /Dégâts, indisponible/ })).toBeDisabled();
  });

  it("change de métrique avec le sélecteur", async () => {
    await ouvrirCourbes(recap(chronologie()));

    await userEvent.click(screen.getByRole("button", { name: /Kills, réel/ }));

    expect(screen.getByRole("heading", { name: "Avantage de kills" })).toBeInTheDocument();
    // 8 kills côté bleu, 4 côté rouge.
    expect(graphe(/Avantage de kills/)).toHaveAccessibleName(/\+4/);
  });

  it("montre les marqueurs d'objectifs avec leur heure", async () => {
    await ouvrirCourbes(recap(chronologie()));

    expect(screen.getByRole("img", { name: "Dragon bleu à 5:00" })).toBeInTheDocument();
    expect(screen.getByRole("img", { name: "Tourelle rouge à 15:00" })).toBeInTheDocument();
  });

  it("remplace la lettre du marqueur par le logo de l'objectif", async () => {
    await ouvrirCourbes(recap(chronologie()));

    const marqueur = screen.getByRole("img", { name: "Dragon bleu à 5:00" });
    expect(marqueur.textContent).toBe("");
    expect(marqueur.querySelector("image")).toHaveAttribute(
      "href",
      "https://raw.communitydragon.org/latest/plugins/rcp-fe-lol-match-history/global/default/dragon-100.png",
    );
  });

  it("affiche une infobulle au survol d'un marqueur, puis la retire", async () => {
    await ouvrirCourbes(recap(chronologie()));
    const marqueur = screen.getByRole("img", { name: "Dragon bleu à 5:00" });
    expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();

    await userEvent.hover(marqueur);
    expect(screen.getByRole("tooltip")).toHaveTextContent("Dragon · équipe bleue · 5:00");

    await userEvent.unhover(marqueur);
    expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
  });

  it("affiche l'infobulle au focus clavier d'un marqueur", async () => {
    await ouvrirCourbes(recap(chronologie()));
    const marqueur = screen.getByRole("img", { name: "Tourelle rouge à 15:00" });

    act(() => marqueur.focus());
    expect(screen.getByRole("tooltip")).toHaveTextContent("Tourelle · équipe rouge · 15:00");
    expect(marqueur).toHaveAttribute("aria-describedby", screen.getByRole("tooltip").id);

    act(() => marqueur.blur());
    expect(screen.queryByRole("tooltip")).not.toBeInTheDocument();
  });

  it("bascule sur la courbe d'un joueur, puis revient aux équipes", async () => {
    await ouvrirCourbes(recap(chronologie()));

    const bob = screen.getByRole("button", { name: /Bob/ });
    await userEvent.click(bob);

    expect(bob).toHaveAttribute("aria-pressed", "true");
    expect(screen.getByRole("heading", { name: "Or de Bob" })).toBeInTheDocument();
    expect(graphe(/Or de Bob/)).toHaveAccessibleName(/7\s?000/);

    await userEvent.click(bob);
    expect(screen.getByRole("heading", { name: "Avantage d'or" })).toBeInTheDocument();
  });

  it("affiche trois petits graphes (kills, CS, niveau moyen) avec la valeur finale de chaque équipe, sans légende", async () => {
    await ouvrirCourbes(recap(chronologie()));

    const kills = tuile("Kills");
    expect(kills).toHaveTextContent("8");
    expect(kills).toHaveTextContent("4");
    expect(within(kills).getByText("réel")).toBeInTheDocument();
    expect(tuile("CS")).toHaveTextContent("280");
    expect(tuile("CS")).toHaveTextContent("220");
    expect(tuile("Niveau moyen")).toHaveTextContent("12,5");
    expect(tuile("Niveau moyen")).toHaveTextContent("11,5");
    expect(screen.queryByRole("list", { name: /légende/i })).not.toBeInTheDocument();
  });

  it("grise l'or quand la série est indisponible et choisit alors une autre métrique", async () => {
    const series = SERIES.map((s) => (s.nom === "or" ? { ...s, nature: "indisponible" } : s));
    await ouvrirCourbes(recap(chronologie({ series })));

    expect(screen.getByRole("button", { name: /Or, indisponible/ })).toBeDisabled();
    expect(screen.queryByRole("heading", { name: "Avantage d'or" })).not.toBeInTheDocument();
    expect(screen.getByRole("heading", { name: "Avantage de CS" })).toBeInTheDocument();
  });

  it("n'invente pas de point : un or inconnu ne trace aucune courbe", async () => {
    const base = chronologie();
    const points = base.points.map((p) => ({
      ...p,
      joueurs: p.joueurs.map((j) => (j.playerId === 2 ? { ...j, orEstime: null } : j)),
    }));
    await ouvrirCourbes(recap({ ...base, points }));

    expect(screen.getByText(/pas assez de données/i)).toBeInTheDocument();
    expect(screen.queryByRole("img", { name: /Avantage d'or/ })).not.toBeInTheDocument();
  });
});
