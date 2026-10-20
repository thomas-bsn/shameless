import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import type { Schemas } from "@/api/client";
import { mockApi, problem } from "@/test/api";
import { ORIGINE_VIDE, STATS_VIDES, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

type Details = Schemas["DetailsDuJoueur"];

const catalogue: Schemas["CatalogueDeJeu"] = {
  version: "16.1.1",
  champions: [],
  objets: [
    { id: 3006, name: "Bottes de berserker", iconUrl: "https://dd/3006.png" },
    { id: 3047, name: "Sandales de Mercure", iconUrl: "https://dd/3047.png" },
    { id: 1205, name: "Quête du rôle terminée", iconUrl: "https://dd/1205.png" },
    { id: 1202, name: "Quête du rôle en cours", iconUrl: "https://dd/1202.png" },
  ],
  sorts: [
    { id: 4, name: "Flash", iconUrl: "https://dd/flash.png" },
    { id: 14, name: "Embrasement", iconUrl: "https://dd/ignite.png" },
  ],
  arbres: [
    {
      id: 8100,
      name: "Domination",
      iconUrl: "https://dd/domination.png",
      runes: [
        { id: 8112, name: "Électrocution", iconUrl: "https://dd/electrocute.png" },
        { id: 8126, name: "Coup bas", iconUrl: "https://dd/cheap.png" },
      ],
    },
    { id: 8300, name: "Inspiration", iconUrl: "https://dd/inspiration.png", runes: [] },
  ],
};

const details = (overrides: Partial<Details> = {}): Details => ({
  role: "Mid",
  sourceDuRole: "Partie",
  items: [3006, 3047, null, null, null, null, null],
  objetDeRole: { id: 1205, etat: "Terminee" },
  spell1Id: 4,
  spell2Id: 14,
  perks: [8112, 8126, null, null, null, null],
  perkPrimaryStyle: 8100,
  perkSubStyle: 8300,
  csParMinute: 7.5,
  orParMinute: 420,
  degatsParMinute: 700,
  visionParMinute: 1.2,
  partDesDegats: 0.4,
  partDeLOr: 0.3,
  partDesKills: 0.65,
  sortsLances: { q: 118, w: 87, e: 142, r: 9, d: 3, f: 2 },
  pings: {
    allIn: 0, assistMe: 10, basic: 0, command: 7, danger: 0, enemyMissing: 1, enemyVision: 0,
    getBack: 0, hold: 0, needVision: 0, onMyWay: 14, push: 0, retreat: 0, visionCleared: 0,
  },
  ...overrides,
});

const stats = (overrides: Partial<Schemas["StatsDuParticipant"]> = {}): Schemas["StatsDuParticipant"] => ({
  ...STATS_VIDES,
  kills: 7,
  deaths: 2,
  assists: 9,
  minionsKilled: 200,
  neutralMinionsKilled: 20,
  damageToChampions: 21000,
  physicalDamageToChampions: 15000,
  magicDamageToChampions: 5000,
  trueDamageToChampions: 1000,
  goldEarned: 14200,
  wardsPlaced: 11,
  wardsKilled: 4,
  controlWardsBought: 3,
  champLevel: 16,
  position: "Mid",
  ...overrides,
});

const participant = (
  playerId: number,
  prenom: string,
  overrides: Partial<Schemas["GameHistoryParticipantDto"]> = {},
): Schemas["GameHistoryParticipantDto"] => ({
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  avatarUrl: null,
  rankTier: "GOLD",
  rankDivision: 2,
  cost: 3,
  championId: null,
  stats: stats(),
  performance: { note: 80, role: "Mid", sourceDuRole: "Partie", composantes: [], noteOrigine: null },
  champsCorriges: [],
  details: details(),
  place: null,
  badge: null,
  ...ORIGINE_VIDE,
  ...overrides,
});

const objectifs = (overrides: Partial<Schemas["ObjectifsDEquipe"]> = {}): Schemas["ObjectifsDEquipe"] => ({
  dragonKills: 3,
  baronKills: 1,
  riftHeraldKills: 2,
  hordeKills: 4,
  towerKills: 9,
  inhibitorKills: 1,
  firstBlood: true,
  firstTower: false,
  firstInhibitor: true,
  ...overrides,
});

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
  teamAObjectifs: objectifs({ dragonKills: 1 }),
  teamBObjectifs: objectifs(),
  teamA: [
    participant(1, "Alice", { championId: "Ahri", place: 4, badge: "Ace" }),
    participant(3, "Chloé", {
      championId: "Jinx",
      place: 7,
      details: details({
        role: "Adc",
        items: [null, null, null, null, null, null, null],
        objetDeRole: { id: 1202, etat: "EnCours" },
        spell1Id: 999,
        perks: [777, null, null, null, null, null],
        sortsLances: null,
        pings: null,
      }),
    }),
  ],
  teamB: [
    participant(2, "Bob", { championId: "Darius", place: 1, badge: "MeilleurePerf", details: details({ role: "Top" }) }),
    participant(4, "Dan", { championId: "Leona", place: 10, details: details({ role: "Support", objetDeRole: null }) }),
  ],
  ...overrides,
});

const ligne = (prenom: string) => screen.getByRole("row", { name: new RegExp(prenom) });

function cellule(row: HTMLElement, colonne: string) {
  const table = row.closest("table") as HTMLElement;
  const index = within(table)
    .getAllByRole("columnheader")
    .findIndex((h) => h.textContent === colonne);
  return within(row).getAllByRole("cell")[index];
}

function ouvrirApp(game = recap(), routes: Record<string, object | Response> = { "GET /catalogue": catalogue }) {
  mockApi({ "GET /auth/me": user(), "GET /games/42": game, ...routes });
  renderApp("/games/42");
}

describe("Page d'une game : onglets", () => {
  it("propose Général, Détails, Runes et Replay, Général d'abord", async () => {
    ouvrirApp();

    expect(await screen.findByRole("tab", { name: "Général" })).toHaveAttribute("data-state", "active");
    expect(screen.getByRole("tab", { name: "Détails" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Runes" })).toBeInTheDocument();
    expect(screen.getByRole("tab", { name: "Replay" })).toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "Résumé" })).not.toBeInTheDocument();
  });
});

describe("Onglet Général", () => {
  it("met le rôle joué à gauche du joueur, avec CS/min, or, participation aux kills et dégâts", async () => {
    ouvrirApp();

    await screen.findByRole("table", { name: "Équipe A · Red side" });
    const alice = ligne("Alice");
    expect(cellule(alice, "Rôle")).toHaveTextContent("Mid");
    expect(alice.querySelector("td")).toBe(cellule(alice, "Rôle"));
    expect(cellule(ligne("Chloé"), "Rôle")).toHaveTextContent("ADC");
    expect(cellule(alice, "K/D/A")).toHaveTextContent("7/2/9");
    expect(cellule(alice, "CS")).toHaveTextContent("220");
    expect(cellule(alice, "CS")).toHaveTextContent("7,5 /min");
    expect(cellule(alice, "Or")).toHaveTextContent("14,2 k");
    expect(cellule(alice, "Or")).toHaveTextContent("420 /min");
    expect(cellule(alice, "PK")).toHaveTextContent("65 %");
    expect(cellule(alice, "Dégâts")).toHaveTextContent("21,0 k");
    expect(cellule(alice, "Note")).toHaveTextContent("80/100");
  });

  it("montre les sorts et les objets avec leurs icônes, les emplacements vides restant neutres", async () => {
    ouvrirApp();

    await screen.findByRole("table", { name: "Équipe A · Red side" });
    const alice = ligne("Alice");
    expect(await within(cellule(alice, "Sorts")).findByRole("img", { name: "Flash" })).toHaveAttribute("src", "https://dd/flash.png");
    expect(within(cellule(alice, "Sorts")).getByRole("img", { name: "Embrasement" })).toBeInTheDocument();
    const objets = within(cellule(alice, "Objets"));
    expect(objets.getByRole("img", { name: "Bottes de berserker" })).toHaveAttribute("src", "https://dd/3006.png");
    expect(objets.getByRole("img", { name: "Sandales de Mercure" })).toBeInTheDocument();
    expect(objets.getAllByRole("listitem")).toHaveLength(7);
  });

  it("place l'objet de rôle à part, avec l'état de sa quête", async () => {
    ouvrirApp();

    await screen.findByRole("table", { name: "Équipe A · Red side" });
    const fini = within(cellule(ligne("Alice"), "Objets")).getByRole("group", { name: "Objet de rôle" });
    expect(await within(fini).findByRole("img", { name: "Quête du rôle terminée" })).toBeInTheDocument();
    expect(fini).toHaveTextContent("Quête terminée");
    const encours = within(cellule(ligne("Chloé"), "Objets")).getByRole("group", { name: "Objet de rôle" });
    expect(encours).toHaveTextContent("Quête en cours");
    expect(within(cellule(ligne("Dan"), "Objets")).queryByRole("group", { name: "Objet de rôle" })).not.toBeInTheDocument();
  });

  it("affiche la place et les badges Meilleure perf et ACE", async () => {
    ouvrirApp();

    await screen.findByRole("table", { name: "Équipe A · Red side" });
    expect(cellule(ligne("Bob"), "Note")).toHaveTextContent("#1");
    expect(within(ligne("Bob")).getByText("Meilleure perf")).toBeInTheDocument();
    expect(within(ligne("Alice")).getByText("ACE")).toBeInTheDocument();
    expect(cellule(ligne("Dan"), "Note")).toHaveTextContent("#10");
    expect(within(ligne("Dan")).queryByText("ACE")).not.toBeInTheDocument();
    expect(within(ligne("Dan")).queryByText("Meilleure perf")).not.toBeInTheDocument();
  });

  it("affiche les objectifs des deux équipes", async () => {
    ouvrirApp();

    const bleue = await screen.findByRole("group", { name: "Objectifs de l'Équipe B" });
    expect(within(bleue).getByText("Dragons").nextSibling).toHaveTextContent("3");
    expect(bleue).toHaveTextContent(/Tourelles\s*9/);
    expect(bleue).toHaveTextContent("Premier sang");
    const rouge = screen.getByRole("group", { name: "Objectifs de l'Équipe A" });
    expect(within(rouge).getByText("Dragons").nextSibling).toHaveTextContent("1");
  });

  it("affiche les ids sans erreur quand le catalogue répond 503", async () => {
    ouvrirApp(recap(), { "GET /catalogue": problem(503) });

    await screen.findByRole("table", { name: "Équipe A · Red side" });
    expect(await within(cellule(ligne("Alice"), "Objets")).findByText("3006")).toBeInTheDocument();
    expect(within(cellule(ligne("Alice"), "Sorts")).getByText("4")).toBeInTheDocument();
    expect(screen.queryByText(/erreur|indisponible|503|unavailable/i)).not.toBeInTheDocument();
  });

  it("affiche l'id d'un élément que le catalogue ne connaît pas", async () => {
    ouvrirApp();

    await screen.findByRole("table", { name: "Équipe A · Red side" });
    expect(await within(cellule(ligne("Chloé"), "Sorts")).findByText("999")).toBeInTheDocument();
  });

  it("n'affiche aucune erreur pour une game saisie à la main", async () => {
    const base = recap();
    ouvrirApp(
      recap({
        riotMatchId: null,
        match: null,
        teamAObjectifs: null,
        teamBObjectifs: null,
        teamA: [participant(1, "Alice", { stats: null, performance: null, details: null })],
        teamB: [participant(2, "Bob", { stats: null, performance: null, details: null })],
        winner: base.winner,
      }),
    );

    expect(await screen.findByText(/Résultat saisi à la main/)).toBeInTheDocument();
    expect(screen.queryByText(/erreur|indisponible|503|unavailable/i)).not.toBeInTheDocument();
    for (const onglet of ["Détails", "Runes"]) {
      await userEvent.click(screen.getByRole("tab", { name: onglet }));
      expect(screen.getByText(/Résultat saisi à la main/)).toBeInTheDocument();
    }
  });

  it("supporte une game avec Rapport mais sans détails (données d'avant l'enrichissement)", async () => {
    ouvrirApp(
      recap({
        teamAObjectifs: null,
        teamBObjectifs: null,
        teamA: [participant(1, "Alice", { details: null })],
        teamB: [participant(2, "Bob", { details: null })],
      }),
    );

    await screen.findByRole("table", { name: "Équipe A · Red side" });
    expect(cellule(ligne("Alice"), "CS")).toHaveTextContent("220");
    expect(cellule(ligne("Alice"), "Rôle")).toHaveTextContent("Mid");
    expect(screen.queryByText(/erreur|indisponible|503|unavailable/i)).not.toBeInTheDocument();
  });
});

describe("Onglet Détails", () => {
  it("détaille le joueur choisi parmi les joueurs de la game", async () => {
    ouvrirApp();
    await userEvent.click(await screen.findByRole("tab", { name: "Détails" }));

    const selecteur = screen.getByRole("group", { name: "Joueur" });
    expect(within(selecteur).getAllByRole("button")).toHaveLength(4);

    // Le premier joueur de la liste (côté bleu d'abord) est choisi d'office ; on passe sur Alice.
    expect(screen.getByRole("group", { name: "Détails de Bob" })).toBeInTheDocument();
    await userEvent.click(within(selecteur).getByRole("button", { name: "Alice" }));
    const alice = screen.getByRole("group", { name: "Détails de Alice" });
    expect(alice).toHaveTextContent(/Balises posées\s*11/);
    expect(alice).toHaveTextContent(/Balises détruites\s*4/);
    expect(alice).toHaveTextContent(/Balises de contrôle\s*3/);
    expect(alice).toHaveTextContent(/CS par minute\s*7,5/);
    expect(alice).toHaveTextContent(/Or par minute\s*420/);
    expect(alice).toHaveTextContent(/Dégâts par minute\s*700/);
    expect(alice).toHaveTextContent(/Vision par minute\s*1,2/);
    expect(alice).toHaveTextContent(/Dégâts physiques\s*15,0 k/);
    expect(alice).toHaveTextContent(/Dégâts magiques\s*5,0 k/);
    expect(alice).toHaveTextContent(/Dégâts bruts\s*1,0 k/);

    await userEvent.click(within(selecteur).getByRole("button", { name: "Bob" }));
    expect(screen.getByRole("group", { name: "Détails de Bob" })).toBeInTheDocument();
    expect(screen.queryByRole("group", { name: "Détails de Alice" })).not.toBeInTheDocument();
  });

  it("montre les six sorts lancés et les pings non nuls du joueur choisi", async () => {
    ouvrirApp();
    await userEvent.click(await screen.findByRole("tab", { name: "Détails" }));
    await userEvent.click(within(screen.getByRole("group", { name: "Joueur" })).getByRole("button", { name: "Alice" }));

    const alice = screen.getByRole("group", { name: "Détails de Alice" });
    const sorts = within(alice).getByRole("group", { name: "Sorts lancés" });
    for (const [touche, valeur] of [["Q", "118"], ["W", "87"], ["E", "142"], ["R", "9"], ["D", "3"], ["F", "2"]]) {
      expect(sorts).toHaveTextContent(new RegExp(`${touche}\\s*${valeur}(?!\\d)`));
    }
    const pings = within(alice).getByRole("group", { name: "Pings" });
    expect(pings).toHaveTextContent(/En route\s*14/);
    expect(pings).toHaveTextContent(/À l'aide\s*10/);
    expect(pings).toHaveTextContent(/Commande\s*7/);
    expect(pings).toHaveTextContent(/Ennemi disparu\s*1/);
    expect(pings).not.toHaveTextContent(/Danger/);
    expect(pings).not.toHaveTextContent(/Retraite/);
  });

  it("n'affiche ni sorts lancés ni pings pour un joueur dont le Rapport n'en porte pas", async () => {
    ouvrirApp();
    await userEvent.click(await screen.findByRole("tab", { name: "Détails" }));
    await userEvent.click(within(screen.getByRole("group", { name: "Joueur" })).getByRole("button", { name: "Chloé" }));

    const chloe = screen.getByRole("group", { name: "Détails de Chloé" });
    expect(within(chloe).queryByRole("group", { name: "Sorts lancés" })).not.toBeInTheDocument();
    expect(within(chloe).queryByRole("group", { name: "Pings" })).not.toBeInTheDocument();
    expect(chloe).toHaveTextContent(/Balises posées/);
  });

  it("n'affiche ni ordre d'achat, ni ordre des compétences, ni phase de lane", async () => {
    ouvrirApp();
    await userEvent.click(await screen.findByRole("tab", { name: "Détails" }));

    expect(screen.queryByText(/ordre d'achat/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/ordre des compétences/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/phase de lane/i)).not.toBeInTheDocument();
  });

  it("garde la comparaison des équipes sur les dégâts, les objectifs et la vision", async () => {
    ouvrirApp();
    await userEvent.click(await screen.findByRole("tab", { name: "Détails" }));

    expect(screen.getByRole("group", { name: "Dégâts infligés" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Objectifs" }));
    expect(screen.getByRole("group", { name: "Tours" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Vision" }));
    expect(screen.getByRole("group", { name: "Score de vision" })).toBeInTheDocument();
  });
});

describe("Onglet Runes", () => {
  it("montre les runes et les arbres des joueurs avec leurs icônes", async () => {
    ouvrirApp();
    await userEvent.click(await screen.findByRole("tab", { name: "Runes" }));

    const alice = screen.getByRole("group", { name: "Runes de Alice" });
    expect(await within(alice).findByRole("img", { name: "Électrocution" })).toHaveAttribute("src", "https://dd/electrocute.png");
    expect(within(alice).getByRole("img", { name: "Coup bas" })).toBeInTheDocument();
    expect(within(alice).getByRole("img", { name: "Domination" })).toBeInTheDocument();
    expect(within(alice).getByRole("img", { name: "Inspiration" })).toBeInTheDocument();
    for (const prenom of ["Alice", "Chloé", "Bob", "Dan"]) {
      expect(screen.getByRole("group", { name: `Runes de ${prenom}` })).toBeInTheDocument();
    }
    expect(within(screen.getByRole("group", { name: "Runes de Chloé" })).getByText("777")).toBeInTheDocument();
  });

  it("affiche les ids quand le catalogue répond 503", async () => {
    ouvrirApp(recap(), { "GET /catalogue": problem(503) });
    await userEvent.click(await screen.findByRole("tab", { name: "Runes" }));

    expect(await within(screen.getByRole("group", { name: "Runes de Alice" })).findByText("8112")).toBeInTheDocument();
    expect(screen.queryByText(/erreur|indisponible|503|unavailable/i)).not.toBeInTheDocument();
  });
});
