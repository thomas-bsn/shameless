import { screen, within } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { mockApi, problem } from "@/test/api";
import { ORIGINE_VIDE, STATS_VIDES, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

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
  stats: null,
  performance: null,
  champsCorriges: [], details: null, place: null, badge: null,
  ...ORIGINE_VIDE,
  ...overrides,
});

const stats = (
  kills: number,
  deaths: number,
  assists: number,
  overrides: Partial<Schemas["StatsDuParticipant"]> = {},
): Schemas["StatsDuParticipant"] => ({
  ...STATS_VIDES,
  kills,
  deaths,
  assists,
  minionsKilled: 180,
  neutralMinionsKilled: 20,
  damageToChampions: 21000,
  damageTaken: null,
  goldEarned: 14200,
  visionScore: null,
  wardsPlaced: null,
  wardsKilled: null,
  controlWardsBought: null,
  damageToObjectives: null,
  turretKills: null,
  dragonKills: null,
  baronKills: null,
  champLevel: 16,
  position: "Top",
  ...overrides,
});

const note = (value: number): Schemas["NoteDePerformance"] => ({
  note: value,
  role: "Top",
  sourceDuRole: "Partie",
  composantes: [],
  noteOrigine: null,
});

const recap = (overrides: Partial<Schemas["GameRecapResponse"]> = {}): Schemas["GameRecapResponse"] => ({
  id: 42,
  seriesId: 5,
  gameNumber: 2,
  bestOf: 3,
  status: "completed",
  playedAt: "2026-10-16T20:30:00Z",
  completedAt: "2026-10-16T21:05:00Z",
  modifieeParAdminLe: null,
  teamASide: "red",
  teamBSide: "blue",
  winner: "B",
  teamAWins: 1,
  teamBWins: 1,
  seriesOutcome: "EnCours",
  riotMatchId: "EUW1_1",
  match: { durationSeconds: 1834, gameMode: "CLASSIC", gameType: "CUSTOM_GAME", gameVersion: null }, teamAObjectifs: null, teamBObjectifs: null,
  teamA: [
    participant(1, "Alice", { championId: "Ahri", stats: stats(7, 2, 9), performance: note(86) }),
    participant(3, "Chloé", { championId: "Jinx", stats: stats(1, 5, 3, { goldEarned: null }), performance: note(40) }),
  ],
  teamB: [
    participant(2, "Bob", { championId: "Darius", stats: stats(3, 5, 2), performance: note(62) }),
    participant(4, "Dan", { championId: "Leona", stats: stats(0, 7, 3, { champLevel: null }), performance: note(31) }),
  ],
  ...overrides,
});

const votes = (overrides: Partial<Schemas["ResultatsDesVotes"]> = {}): Schemas["ResultatsDesVotes"] => ({
  gameId: 42,
  statut: "clos",
  ouvertureLe: "2026-10-16T21:05:00Z",
  fermetureLe: "2026-10-16T21:15:00Z",
  votants: 4,
  mvp: { gagnants: [1], decompte: [{ playerId: 1, prenom: "Alice", voix: 3, avatarUrl: null }] },
  dofd: { gagnants: [4], decompte: [{ playerId: 4, prenom: "Dan", voix: 2, avatarUrl: null }] },
  ...overrides,
});

const ligne = (prenom: string) => screen.getByRole("listitem", { name: prenom });

describe("Page d'une Game", () => {
  it("présente le duel des deux équipes : noms, issue, score de kills, durée, Série et date", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/42": recap() });

    renderApp("/games/42");

    expect(await screen.findByRole("heading", { name: "Victoire de l'Équipe B · 30:34" })).toBeInTheDocument();
    const entete = screen.getByRole("region", { name: "En-tête de la game" });
    const bleue = within(entete).getByRole("group", { name: "Équipe B (côté bleu)" });
    expect(bleue).toHaveTextContent("Équipe B");
    expect(bleue).toHaveTextContent("Victoire");
    const rouge = within(entete).getByRole("group", { name: "Équipe A (côté rouge)" });
    expect(rouge).toHaveTextContent("Équipe A");
    expect(rouge).toHaveTextContent("Défaite");
    expect(within(rouge).getAllByRole("img")).toHaveLength(2);
    const centre = within(entete).getByRole("group", { name: "Score" });
    expect(centre).toHaveTextContent("3 : 8");
    expect(centre).toHaveTextContent("30:34");
    expect(centre).toHaveTextContent("Game 2 · BO3 · 1 – 1");
    expect(centre).toHaveTextContent(/16 oct/);
    expect(within(entete).getByRole("link", { name: "Voir la Série" })).toHaveAttribute("href", "/series/5");
    expect(screen.getByRole("tab", { name: "Général" })).toBeInTheDocument();
  });

  it("masque le score de kills quand les stats ne les donnent pas, sans rien inventer", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap({
        match: null,
        teamA: [participant(1, "Alice", { championId: "Ahri" })],
        teamB: [participant(2, "Bob", { championId: "Darius" })],
      }),
    });

    renderApp("/games/42");

    const centre = await screen.findByRole("group", { name: "Score" });
    expect(centre).not.toHaveTextContent(/\d+ : \d+/);
    expect(centre).not.toHaveTextContent("30:34");
    expect(centre).toHaveTextContent("Game 2 · BO3 · 1 – 1");
  });

  it("affiche les joueurs des deux équipes dans deux tableaux avec leurs stats et leur note sur 100", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/42": recap() });

    renderApp("/games/42");

    const rouge = await screen.findByRole("list", { name: "Équipe A · Red side" });
    const bleu = screen.getByRole("list", { name: "Équipe B · Blue side" });
    expect(within(rouge).getAllByRole("listitem", { name: /Alice|Chloé/ })).toHaveLength(2);
    expect(within(bleu).getAllByRole("listitem", { name: /Bob|Dan/ })).toHaveLength(2);

    const alice = ligne("Alice");
    expect(within(alice).getByRole("link", { name: "Alice" })).toBeInTheDocument();
    expect(alice).toHaveTextContent("7/2/9");
    expect(alice).toHaveTextContent("21,0 k");
    expect(within(alice).getByRole("img", { name: "Note 86 sur 100" })).toBeInTheDocument();
    expect(within(alice).getByText("16")).toBeInTheDocument();
  });

  it("affiche une stat absente comme absente, jamais comme zéro", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/42": recap() });

    renderApp("/games/42");

    await screen.findByRole("list", { name: "Équipe A · Red side" });
    expect(ligne("Alice")).toHaveTextContent("–");
    expect(within(ligne("Dan")).queryByText("16")).not.toBeInTheDocument();
    expect(ligne("Dan")).toHaveTextContent("0/7/3");
  });

  it("signale le Roi ou la Reine et le DOFD et détaille les votes", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/42": recap(), "GET /games/42/votes": votes() });

    renderApp("/games/42");

    const alice = await screen.findByRole("listitem", { name: "Alice" });
    expect(await within(alice).findByText("Roi/Reine")).toBeInTheDocument();
    expect(await within(await screen.findByRole("listitem", { name: "Dan" })).findByText("DOFD")).toBeInTheDocument();
    expect(within(ligne("Bob")).queryByText("Roi/Reine")).not.toBeInTheDocument();

    const mvp = screen.getByRole("group", { name: "MVP" });
    expect(within(mvp).getByText("3 voix")).toBeInTheDocument();
  });

  it("affiche les pronostics et les clips de la Game", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap(),
      "GET /games/42/pronostics": {
        gameId: 42,
        gameNumber: 2,
        vainqueurConnu: true,
        votesClos: true,
        pronostics: [
          {
            pronostiqueurDiscordId: "1234",
            pronostiqueurNom: "Léa",
            equipeGagnante: { equipe: "B", correct: true },
            mvp: null,
            dofd: null,
          },
        ],
      },
      "GET /games/42/clips": [
        {
          id: 7,
          gameId: 42,
          rattachement: "automatique",
          auteurNom: "Sam",
          auteurDiscordId: "1",
          publieLe: "2026-10-16T21:10:00Z",
          nomFichier: "penta.mp4",
          contentType: "video/mp4",
          taille: 1000,
        },
      ],
    });

    renderApp("/games/42");

    expect(await screen.findByRole("group", { name: "Pronostic de Léa" })).toBeInTheDocument();
    expect(await screen.findByRole("figure", { name: "penta.mp4" })).toBeInTheDocument();
  });

  it("explique qu'une Game saisie à la main n'a pas de stats, et garde équipes, vainqueur et votes", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap({
        riotMatchId: null,
        match: null,
        teamA: [participant(1, "Alice")],
        teamB: [participant(2, "Bob")],
      }),
      "GET /games/42/votes": votes({ statut: "ouverts", mvp: { gagnants: [], decompte: [] }, dofd: { gagnants: [], decompte: [] } }),
    });

    renderApp("/games/42");

    expect(await screen.findByRole("heading", { name: "Victoire de l'Équipe B" })).toBeInTheDocument();
    expect(screen.getByText(/Résultat saisi à la main/)).toBeInTheDocument();
    expect(screen.queryByRole("table")).not.toBeInTheDocument();
    expect(within(screen.getByRole("group", { name: "Équipe A · Red side" })).getByText("Alice")).toBeInTheDocument();
    expect(within(screen.getByRole("group", { name: "Équipe B · Blue side" })).getByText("Bob")).toBeInTheDocument();
    expect(await screen.findByRole("heading", { name: "Votes" })).toBeInTheDocument();
  });

  it("montre une Game en attente sans vainqueur", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap({ status: "pending", winner: null, completedAt: null, match: null }),
    });

    renderApp("/games/42");

    expect(await screen.findByText("En cours")).toBeInTheDocument();
    expect(screen.queryByText(/Victoire/)).not.toBeInTheDocument();
    expect(screen.queryByText(/saisi à la main/)).not.toBeInTheDocument();
  });

  it("signale une Game inconnue", async () => {
    mockApi({ "GET /auth/me": user(), "GET /games/42": problem(404, "Cette game n'existe pas.") });

    renderApp("/games/42");

    expect(await screen.findByText("Cette game n'existe pas.")).toBeInTheDocument();
  });
});
