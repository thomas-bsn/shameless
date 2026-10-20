import { screen, within } from "@testing-library/react";
import type { Schemas } from "@/api/client";
import { mockApi } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const recap = (overrides: Partial<Schemas["GameRecapResponse"]> = {}): Schemas["GameRecapResponse"] => ({
  id: 42,
  seriesId: 5,
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
  seriesOutcome: "EnCours",
  riotMatchId: null,
  match: null, teamAObjectifs: null, teamBObjectifs: null,
  teamA: [],
  teamB: [],
  ...overrides,
});

const votes = (overrides: Partial<Schemas["ResultatsDesVotes"]> = {}): Schemas["ResultatsDesVotes"] => ({
  gameId: 42,
  statut: "clos",
  ouvertureLe: "2026-10-16T21:05:00Z",
  fermetureLe: "2026-10-16T21:15:00Z",
  votants: 4,
  mvp: { gagnants: [], decompte: [] },
  dofd: { gagnants: [], decompte: [] },
  ...overrides,
});

const statsVides: Schemas["StatsDeCustoms"] = {
  gamesJouees: 0,
  victoires: 0,
  defaites: 0,
  winrate: null,
  kda: null,
  killsMoyens: null,
  mortsMoyennes: null,
  assistsMoyens: null,
  csParMinute: null,
  participationAuxKills: null,
  degatsMoyens: null,
  degatsParMinute: null,
  noteMoyenne: null,
  gamesNotees: 0,
};

const fiche = (titres: Schemas["TitresDuJoueur"]): Schemas["FicheJoueur"] => ({
  playerId: 7,
  prenom: "Kélio",
  riotId: "Kelio#EUW",
  discord: null,
  comptesDiscord: [],
  cost: 3,
  soloQ: null,
  flex: null,
  stats: statsVides,
  champions: [],
  roles: [],
  games: [],
  titres,
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
});

describe("Votes MVP et DOFD", () => {
  it("montre dans le récap les gagnants, tous en cas d'égalité, et le décompte des voix", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap(),
      "GET /games/42/votes": votes({
        mvp: {
          gagnants: [1, 2],
          decompte: [
            { playerId: 1, prenom: "Alice", voix: 2, avatarUrl: null },
            { playerId: 2, prenom: "Bob", voix: 2, avatarUrl: null },
            { playerId: 3, prenom: "Chloé", voix: 1, avatarUrl: null },
          ],
        },
        dofd: { gagnants: [4], decompte: [{ playerId: 4, prenom: "Dan", voix: 3, avatarUrl: null }] },
      }),
    });

    renderApp("/games/42");

    const mvp = await screen.findByRole("group", { name: "MVP" });
    expect(within(mvp).getAllByRole("listitem").map((li) => li.textContent)).toEqual([
      expect.stringContaining("Alice"),
      expect.stringContaining("Bob"),
      expect.stringContaining("Chloé"),
    ]);
    expect(within(mvp).getAllByText("Gagnant")).toHaveLength(2);
    expect(within(mvp).getByText("1 voix")).toBeInTheDocument();
    const dofd = screen.getByRole("group", { name: "DOFD" });
    expect(within(dofd).getByText("Dan")).toBeInTheDocument();
    expect(within(dofd).getByText("3 voix")).toBeInTheDocument();
    expect(screen.getByText("Votes clos · 4 votants")).toBeInTheDocument();
  });

  it("annonce les votes en cours sans dévoiler gagnants ni décompte", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap(),
      "GET /games/42/votes": votes({ statut: "ouverts", votants: 1 }),
    });

    renderApp("/games/42");

    expect(await screen.findByText(/Votes ouverts dans Discord jusqu'à .* · 1 votant$/)).toBeInTheDocument();
    expect(screen.getByText("Les gagnants et le décompte des voix seront dévoilés à la clôture.")).toBeInTheDocument();
    expect(screen.queryByRole("group", { name: "MVP" })).not.toBeInTheDocument();
    expect(screen.queryByRole("group", { name: "DOFD" })).not.toBeInTheDocument();
  });

  it("annonce l'ouverture des votes à la fin de la Game", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /games/42": recap({ status: "pending", winner: null, completedAt: null }),
      "GET /games/42/votes": votes({ statut: "a_venir", ouvertureLe: null, fermetureLe: null, votants: 0 }),
    });

    renderApp("/games/42");

    expect(await screen.findByText("Les votes ouvriront dans Discord à la fin de la Game.")).toBeInTheDocument();
  });

  it("montre les titres de MVP et de DOFD sur la fiche du Joueur", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players/7": player({ id: 7, prenom: "Kélio" }),
      "GET /players/7/fiche": fiche({ mvp: 3, dofd: 1 }),
    });

    renderApp("/joueurs/7");

    const titres = await screen.findByRole("group", { name: "Titres" });
    expect(await within(titres).findByText("3 MVP")).toBeInTheDocument();
    expect(within(titres).getByText("1 DOFD")).toBeInTheDocument();
  });
});
