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

const ligne = (prenom: string) => screen.getByRole("listitem", { name: prenom });

function ouvrirApp(game = recap(), routes: Record<string, object | Response> = { "GET /catalogue": catalogue }) {
  mockApi({ "GET /auth/me": user(), "GET /games/42": game, ...routes });
  renderApp("/games/42");
}

describe("Page d'une game : onglets", () => {
  it("propose Général, Performance, Détails, Runes et Replay 3D, Général d'abord, chacun avec une icône", async () => {
    ouvrirApp();

    const general = await screen.findByRole("tab", { name: "Général" });
    expect(general).toHaveAttribute("data-state", "active");
    const noms = ["Général", "Performance", "Détails", "Runes", "Replay 3D"];
    expect(screen.getAllByRole("tab").map((t) => t.textContent)).toEqual(noms);
    for (const nom of noms) {
      expect(screen.getByRole("tab", { name: nom }).querySelector("svg[aria-hidden='true']")).not.toBeNull();
    }
    expect(screen.queryByRole("tab", { name: "Cibles" })).not.toBeInTheDocument();
    expect(screen.queryByRole("tab", { name: "Résumé" })).not.toBeInTheDocument();
  });

  it("signale l'onglet actif aux lecteurs d'écran", async () => {
    ouvrirApp();

    expect(await screen.findByRole("tab", { name: "Général" })).toHaveAttribute("aria-selected", "true");
    await userEvent.click(screen.getByRole("tab", { name: "Runes" }));
    expect(screen.getByRole("tab", { name: "Runes" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByRole("tab", { name: "Général" })).toHaveAttribute("aria-selected", "false");
  });

  it("garde la barre d'onglets collante en haut de la page", async () => {
    ouvrirApp();

    await screen.findByRole("tab", { name: "Général" });
    expect(screen.getByRole("tablist")).toHaveClass("sticky");
  });
});

const votes = (mvp: number[], dofd: number[]): Schemas["ResultatsDesVotes"] => ({
  gameId: 42,
  statut: "Clos",
  ouvertureLe: null,
  fermetureLe: null,
  votants: 5,
  mvp: { gagnants: mvp, decompte: [] },
  dofd: { gagnants: dofd, decompte: [] },
});

describe("Onglet Général", () => {
  it("met le rôle à gauche en icône avec son nom, dans l'ordre Top, Jungle, Mid, ADC, Support", async () => {
    const base = recap();
    ouvrirApp(
      recap({
        teamB: [
          ...base.teamB,
          participant(5, "Eve", { details: details({ role: "Adc" }), stats: stats({ position: "Adc" }) }),
        ].map((j) => (j.prenom === "Dan" ? { ...j, stats: stats({ position: "Support" }) } : j)),
      }),
    );

    const bleue = await screen.findByRole("list", { name: "Équipe B · Blue side" });
    const roles = within(bleue)
      .getAllByRole("listitem", { name: /Bob|Dan|Eve/ })
      .map((l) => within(l).getAllByRole("img", { name: /^(Top|Jungle|Mid|ADC|Support)$/ })[0]!.getAttribute("aria-label"));
    expect(roles).toEqual(["Top", "ADC", "Support"]);
  });

  it("ouvre la fiche du joueur au clic sur sa ligne, le nom étant un lien vers elle", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/42": recap(), "GET /catalogue": catalogue });
    const { router } = renderApp("/games/42");

    await screen.findByRole("list", { name: "Équipe A · Red side" });
    expect(within(ligne("Alice")).getByRole("link", { name: "Alice" })).toHaveAttribute("href", "/joueurs/1");
    await userEvent.click(within(ligne("Alice")).getByText("7"));
    expect(router.state.location.pathname).toBe("/joueurs/1");
  });

  it("montre place, niveau, rang, K/D/A, participation aux kills et CS/min", async () => {
    ouvrirApp();

    await screen.findByRole("list", { name: "Équipe A · Red side" });
    const alice = ligne("Alice");
    expect(within(alice).getByText("4e")).toBeInTheDocument();
    expect(within(ligne("Bob")).getByText("1er")).toBeInTheDocument();
    expect(within(alice).getByText("16")).toBeInTheDocument();
    expect(alice).toHaveTextContent("Gold II");
    expect(alice).toHaveTextContent("7/2/9");
    expect(alice).toHaveTextContent("65 %");
    expect(alice).toHaveTextContent("7,5");
    expect(within(alice).getByRole("img", { name: "Note 80 sur 100" })).toBeInTheDocument();
    expect(alice).toHaveTextContent("21,0 k");
    expect(alice).toHaveTextContent("700 /min");
  });

  it("montre la vision du Support à la place du CS/min", async () => {
    const base = recap();
    ouvrirApp(
      recap({
        teamB: [
          base.teamB[0]!,
          participant(4, "Dan", {
            championId: "Leona",
            stats: stats({ position: "Support", visionScore: 63 }),
            details: details({ role: "Support", objetDeRole: null }),
          }),
        ],
      }),
    );

    await screen.findByRole("list", { name: "Équipe B · Blue side" });
    const dan = ligne("Dan");
    expect(dan).toHaveTextContent("63");
    expect(dan).toHaveTextContent("vision");
    expect(dan).not.toHaveTextContent("7,5");
    expect(ligne("Bob")).toHaveTextContent("CS/min");
    expect(ligne("Bob")).not.toHaveTextContent("vision");
  });

  it("montre sorts, runes, six objets et babiole avec leurs icônes, les emplacements vides restant neutres", async () => {
    ouvrirApp();

    await screen.findByRole("list", { name: "Équipe A · Red side" });
    const alice = within(ligne("Alice"));
    expect(await alice.findByRole("img", { name: "Flash" })).toHaveAttribute("src", "https://dd/flash.png");
    expect(alice.getByRole("img", { name: "Embrasement" })).toBeInTheDocument();
    expect(alice.getByRole("img", { name: "Électrocution" })).toHaveAttribute("src", "https://dd/electrocute.png");
    expect(alice.getByRole("img", { name: "Inspiration" })).toHaveAttribute("src", "https://dd/inspiration.png");
    const objets = within(alice.getByRole("list", { name: "Objets" }));
    expect(objets.getByRole("img", { name: "Bottes de berserker" })).toHaveAttribute("src", "https://dd/3006.png");
    expect(objets.getAllByRole("listitem")).toHaveLength(7);
  });

  it("montre l'objet de rôle sans texte, son état se lisant à l'icône", async () => {
    ouvrirApp();

    await screen.findByRole("list", { name: "Équipe A · Red side" });
    const fini = within(ligne("Alice")).getByRole("group", { name: "Objet de rôle terminé" });
    expect(await within(fini).findByRole("img", { name: "Quête du rôle terminée" })).toBeInTheDocument();
    const encours = within(ligne("Chloé")).getByRole("group", { name: "Objet de rôle en cours" });
    expect(encours).toBeInTheDocument();
    expect(within(ligne("Dan")).queryByRole("group", { name: /Objet de rôle/ })).not.toBeInTheDocument();
    expect(screen.queryByText(/Quête/)).not.toBeInTheDocument();
  });

  it("montre MVP et ACE d'après les badges, DOFD et Roi ou Reine d'après les votes clos", async () => {
    ouvrirApp(recap(), { "GET /catalogue": catalogue, "GET /games/42/votes": votes([3], [1]) });

    await screen.findByRole("list", { name: "Équipe A · Red side" });
    expect(await within(ligne("Alice")).findByText("DOFD")).toBeInTheDocument();
    expect(within(ligne("Alice")).getByText("ACE")).toBeInTheDocument();
    expect(within(ligne("Bob")).getByText("MVP")).toBeInTheDocument();
    expect(within(ligne("Chloé")).getByText(/Roi/)).toBeInTheDocument();
    expect(within(ligne("Dan")).queryByText("DOFD")).not.toBeInTheDocument();
    expect(within(ligne("Dan")).queryByText("MVP")).not.toBeInTheDocument();
    expect(within(ligne("Dan")).queryByText("ACE")).not.toBeInTheDocument();
  });

  it("met les objectifs de chaque équipe en icônes dans son en-tête", async () => {
    ouvrirApp();

    const bleue = (await screen.findByRole("list", { name: "Équipe B · Blue side" })).closest("section")!;
    const compteur = (nom: string) => within(bleue).getByRole("img", { name: nom }).parentElement;
    expect(compteur("Dragon")).toHaveTextContent("3");
    expect(compteur("Tourelle")).toHaveTextContent("9");
    expect(compteur("Baron")).toHaveTextContent("1");
    expect(compteur("Héraut")).toHaveTextContent("2");
    expect(compteur("Larve du Néant")).toHaveTextContent("4");
    expect(compteur("Inhibiteur")).toHaveTextContent("1");
    const rouge = screen.getByRole("list", { name: "Équipe A · Red side" }).closest("section")!;
    expect(within(rouge).getByRole("img", { name: "Dragon" }).parentElement).toHaveTextContent("1");
  });

  it("allume la barre de dégâts du meilleur de la game seulement", async () => {
    const base = recap();
    ouvrirApp(
      recap({
        teamB: [participant(2, "Bob", { championId: "Darius", stats: stats({ damageToChampions: 40000, position: "Top" }), details: details({ role: "Top" }) }), base.teamB[1]!],
      }),
    );

    await screen.findByRole("list", { name: "Équipe A · Red side" });
    expect(within(ligne("Bob")).getByRole("progressbar", { hidden: true })).toHaveAttribute("data-meilleur", "true");
    expect(within(ligne("Alice")).getByRole("progressbar", { hidden: true })).toHaveAttribute("data-meilleur", "false");
  });

  it("affiche des emplacements neutres sans erreur quand le catalogue répond 503 ou ignore un id", async () => {
    ouvrirApp(recap(), { "GET /catalogue": problem(503) });

    await screen.findByRole("list", { name: "Équipe A · Red side" });
    expect((await within(ligne("Alice")).findAllByRole("img", { name: "Inconnu" })).length).toBeGreaterThan(0);
    expect(screen.queryByText(/erreur|indisponible|503|unavailable/i)).not.toBeInTheDocument();
  });
});

describe("Page d'une game : cas limites", () => {
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

    await screen.findByRole("list", { name: "Équipe A · Red side" });
    expect(within(ligne("Alice")).getByRole("img", { name: "Mid" })).toBeInTheDocument();
    expect(ligne("Alice")).toHaveTextContent("7/2/9");
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
