import { screen, within } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Stats = Schemas["StatsDeCustoms"];
type Fiche = Schemas["FicheJoueur"];

const stats = (overrides: Partial<Stats> = {}): Stats => ({
  gamesJouees: 24,
  victoires: 16,
  defaites: 8,
  winrate: 66.7,
  kda: 3.8,
  killsMoyens: 6,
  mortsMoyennes: 3,
  assistsMoyens: 5,
  csParMinute: 6.9,
  participationAuxKills: 61.2,
  degatsMoyens: 20000,
  degatsParMinute: 812,
  noteMoyenne: 74.4,
  gamesNotees: 20,
  ...overrides,
});

const fiche = (overrides: Partial<Fiche> = {}): Fiche => ({
  playerId: 7,
  prenom: "Aldric",
  riotId: "Aldric#EUW",
  discord: { discordId: "1", username: "aldric", avatarUrl: null, principal: true },
  comptesDiscord: [{ discordId: "1", username: "aldric", avatarUrl: null, principal: true }],
  cost: 6,
  soloQ: { tier: "DIAMOND", division: 2, lp: 45 },
  flex: { tier: "PLATINUM", division: 1, lp: 0 },
  stats: stats(),
  champions: [
    { championId: "Camille", stats: stats({ gamesJouees: 9, winrate: 77.8 }) },
    { championId: "Garen", stats: stats({ gamesJouees: 5, winrate: null }) },
  ],
  roles: [
    { role: "Top", stats: stats({ gamesJouees: 18 }) },
    { role: "Jungle", stats: stats({ gamesJouees: 4 }) },
  ],
  games: [
    {
      gameId: 31,
      seriesId: 5,
      playedAt: "2026-10-17T20:00:00Z",
      equipe: "A",
      victoire: true,
      dureeSecondes: 1800,
      championId: "Camille",
      role: "Top",
      stats: { kills: 9, deaths: 2, assists: 7 } as Schemas["StatsDuParticipant"],
      performance: { note: 86, role: "Top", sourceDuRole: "Position", composantes: [] } as unknown as Schemas["NoteDePerformance"],
      mvp: false,
      dofd: false,
      place: null,
      badge: null,
      participationAuxKills: null,
      adversaireChampionId: null,
    },
    {
      gameId: 30,
      seriesId: null,
      playedAt: "2026-10-16T20:00:00Z",
      equipe: "B",
      victoire: false,
      dureeSecondes: null,
      championId: null,
      role: null,
      stats: null,
      performance: null,
      mvp: false,
      dofd: false,
      place: null,
      badge: null,
      participationAuxKills: null,
      adversaireChampionId: null,
    },
  ],
  titres: { mvp: 3, dofd: 1 },
  joueAvec: [],
  resume: {
    games: 0,
    victoires: 0,
    defaites: 0,
    winrate: null,
    topChampions: [],
    kda: null,
    noteMoyenne: null,
    meilleuresPerfs: 0,
    aces: 0,
    mvp: 0,
    dofd: 0,
  },
  liste: [],
  ...overrides,
});

const historique = (mois: Array<[string, number]>) => ({
  playerId: 7,
  mois: mois.map(([m, cost]) => ({ mois: m, cost, costDeDepart: null, gamesTerminees: 3, victoires: 2, noteMoyenne: null })),
});

const open = (
  f: Fiche = fiche(),
  mois: Array<[string, number]> = [
    ["2026-09-01", 5],
    ["2026-10-01", 6],
  ],
) => {
  mockApi({
    "GET /auth/me": user(),
    "GET /players/7": player({
      id: 7,
      prenom: "Aldric",
      riotId: "Aldric#EUW",
      currentCost: 6,
      rankTier: "DIAMOND",
      rankDivision: 2,
      lp: 45,
      flexRankTier: "PLATINUM",
      flexRankDivision: 1,
      flexLp: 0,
      peakTier: "DIAMOND",
      peakDivision: 1,
      peakLp: 0,
      peakSeason: "S25",
      mainRole: "TOP",
    }),
    "GET /players/7/fiche": f,
    "GET /players/7/cost/historique": historique(mois),
    "GET /champions": [{ id: "Camille", key: "164", name: "Camille", imageUrl: "https://dd/camille.png" }],
  });
  return renderApp("/joueurs/7");
};

describe("Fiche d'un joueur", () => {
  it("montre l'identité, les rangs SoloQ, Flex et peak, les titres et le tier de cost en grand", async () => {
    open();

    expect(await screen.findByRole("heading", { name: "Aldric" })).toBeInTheDocument();
    expect(await screen.findByText(/Discord lié/)).toBeInTheDocument();
    expect(await screen.findByText("Diamond II · 45 LP")).toBeInTheDocument();
    expect(screen.getByText("Platinum I · 0 LP")).toBeInTheDocument();
    expect(screen.getByText("Diamond I · 0 LP")).toBeInTheDocument();
    expect(screen.getByText("S25")).toBeInTheDocument();
    expect(await screen.findByText("3 MVP")).toBeInTheDocument();
    expect(screen.getByText("1 DOFD")).toBeInTheDocument();
    expect(screen.getByText("SS")).toBeInTheDocument();
    expect(screen.getByText("6 pts")).toBeInTheDocument();
  });

  it("affiche les stats en customs, une stat absente comme absente et non comme zéro", async () => {
    open(fiche({ stats: stats({ kda: null, csParMinute: null, degatsParMinute: null, noteMoyenne: null }) }));

    const region = await screen.findByRole("region", { name: /Stats en customs/ });
    expect(within(region).getByText(/24 games/)).toBeInTheDocument();
    expect(within(region).getByText("67 %").parentElement).toHaveTextContent("winrate");
    expect(within(region).getByText("61 %").parentElement).toHaveTextContent("participation aux kills");
    expect(within(region).getByText("KDA").parentElement).toHaveTextContent("–");
    expect(within(region).getByText("CS par minute").parentElement).toHaveTextContent("–");
    expect(within(region).getByText("Note moyenne").parentElement).toHaveTextContent("–");
    expect(within(region).queryByText("0")).not.toBeInTheDocument();
  });

  it("trace la courbe du cost mois par mois sur l'échelle de 1 à 6", async () => {
    open(fiche(), [
      ["2026-08-01", 4],
      ["2026-09-01", 5.5],
      ["2026-10-01", 6],
    ]);

    const courbe = await screen.findByRole("img", { name: /Cost mois par mois/ });
    expect(courbe).toHaveAccessibleName(/août 4/);
    expect(courbe).toHaveAccessibleName(/septembre 5,5/);
    expect(courbe).toHaveAccessibleName(/octobre 6/);
    for (const graduation of ["1", "2", "3", "4", "5", "6"]) {
      expect(within(courbe).getByText(graduation)).toBeInTheDocument();
    }
    expect(courbe.querySelectorAll("circle")).toHaveLength(3);
  });

  it("dit qu'il n'y a pas d'historique de cost plutôt que de tracer une courbe vide", async () => {
    open(fiche(), []);

    expect(await screen.findByText("Pas encore d'historique de cost.")).toBeInTheDocument();
    expect(screen.queryByRole("img", { name: /Cost mois par mois/ })).not.toBeInTheDocument();
  });

  it("liste les champions et les rôles avec leurs games et leur winrate", async () => {
    open();

    const champions = await screen.findByRole("region", { name: "Performance par champion" });
    const camille = within(champions).getByRole("listitem", { name: /Camille/ });
    expect(camille).toHaveTextContent("9 games");
    expect(camille).toHaveTextContent("78 %");
    const garen = within(champions).getByRole("listitem", { name: /Garen/ });
    expect(garen).toHaveTextContent("5 games");
    expect(garen).toHaveTextContent("–");

    const roles = screen.getByRole("region", { name: "Performance par rôle" });
    expect(within(roles).getByRole("listitem", { name: /Top/ })).toHaveTextContent("18 games");
    expect(within(roles).getByRole("listitem", { name: /Jungle/ })).toHaveTextContent("4 games");
  });

  it("liste les games avec leur note sur 100, et une donnée manquante n'est pas un zéro", async () => {
    const f = fiche();
    open({ ...f, liste: [{ soireeId: null, jour: null, games: f.games }] });

    const lignes = await screen.findAllByRole("link", { name: /Voir la game/ });
    expect(lignes).toHaveLength(2);

    expect(lignes[0]).toHaveAttribute("href", "/games/31");
    expect(lignes[0]).toHaveTextContent("9 / 2 / 7");
    expect(lignes[0]).toHaveTextContent("Victoire");
    expect(lignes[0]).toHaveTextContent("86");

    expect(lignes[1]).toHaveTextContent("Défaite");
    expect(lignes[1]).toHaveTextContent("Stats non saisies");
    expect(lignes[1]).not.toHaveTextContent("0/100");
  });

  it("garde l'identité quand la fiche est indisponible", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players/7": player({ id: 7, prenom: "Aldric", riotId: "Aldric#EUW" }),
    });

    renderApp("/joueurs/7");

    expect(await screen.findByRole("heading", { name: "Aldric" })).toBeInTheDocument();
    expect(await screen.findByRole("alert")).toHaveTextContent("Introuvable.");
    expect(screen.queryByRole("region", { name: "Champions" })).not.toBeInTheDocument();
  });
});
