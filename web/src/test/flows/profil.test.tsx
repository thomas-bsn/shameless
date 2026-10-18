import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { json, mockApi, problem } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const joueurs = [player({ id: 1, prenom: "Alice" }), player({ id: 2, prenom: "Bob", riotId: "Bob#EUW" })];

const stats = {
  gamesJouees: 38,
  victoires: 22,
  defaites: 16,
  winrate: 57.9,
  kda: 3.4,
  killsMoyens: 6,
  mortsMoyennes: 4,
  assistsMoyens: 8,
  csParMinute: null,
  participationAuxKills: 62,
  degatsMoyens: null,
  degatsParMinute: null,
  noteMoyenne: 71,
  gamesNotees: 30,
};

const fiche = (overrides = {}) => ({
  playerId: 1,
  prenom: "Alice",
  riotId: "Alice#EUW",
  discord: null,
  cost: 4.6,
  soloQ: null,
  flex: null,
  stats,
  champions: [],
  roles: [],
  games: [
    {
      gameId: 31,
      seriesId: 5,
      playedAt: "2026-10-17T20:00:00Z",
      equipe: "A",
      victoire: true,
      dureeSecondes: 1800,
      championId: "LeeSin",
      role: "Jungle",
      stats: { kills: 11, deaths: 3, assists: 9 },
      performance: { note: 86, role: "Jungle", sourceDuRole: "Position", composantes: [] },
      mvp: true,
      dofd: false,
    },
    {
      gameId: 30,
      seriesId: 5,
      playedAt: "2026-10-16T20:00:00Z",
      equipe: "B",
      victoire: false,
      dureeSecondes: 1700,
      championId: "LeeSin",
      role: "Jungle",
      stats: { kills: 2, deaths: 8, assists: 3 },
      performance: { note: 40, role: "Jungle", sourceDuRole: "Position", composantes: [] },
      mvp: false,
      dofd: true,
    },
    {
      gameId: 29,
      seriesId: 4,
      playedAt: "2026-10-15T20:00:00Z",
      equipe: "A",
      victoire: true,
      dureeSecondes: 1600,
      championId: "LeeSin",
      role: "Jungle",
      stats: { kills: 5, deaths: 4, assists: 6 },
      performance: { note: 60, role: "Jungle", sourceDuRole: "Position", composantes: [] },
      mvp: false,
      dofd: false,
    },
  ],
  titres: { mvp: 5, dofd: 2 },
  ...overrides,
});

const lie = player({
  id: 1,
  currentCost: 4.4,
  costEvolution: 0.3,
  mainRole: "JUNGLE",
  rankTier: "EMERALD",
  rankDivision: 2,
  lp: 47,
  peakTier: "DIAMOND",
  peakDivision: 4,
  peakSeason: "S25",
  peakLp: 10,
});

type Routes = Parameters<typeof mockApi>[0];

const routes = (extra: Routes = {}): Routes => ({
  "GET /auth/me": user({ username: "Sam", role: "Administrateur", bannerColor: "#336699" }),
  "GET /players": joueurs,
  "GET /players/me": lie,
  "GET /players/1/fiche": fiche(),
  "GET /players/1/cost/historique": {
    playerId: 1,
    mois: [
      { mois: "2026-09-01", cost: 4.3, costDeDepart: null, gamesTerminees: 3, victoires: 2, noteMoyenne: null },
      { mois: "2026-10-01", cost: 4.6, costDeDepart: null, gamesTerminees: 3, victoires: 2, noteMoyenne: null },
    ],
  },
  "GET /champions": [{ id: "LeeSin", key: "64", name: "Lee Sin", imageUrl: "https://dd/lee.png" }],
  "GET /players/peak/saisons": { saisons: ["S24", "S25", "S26"], courante: "S26" },
  ...extra,
});

describe("Profil (variante A3)", () => {
  it("montre le Compte Discord avec son badge de rôle, puis le cost et les customs avant la SoloQ", async () => {
    mockApi(routes());
    renderApp("/compte");

    expect(await screen.findByRole("heading", { name: /^Sam\s*Admin$/ })).toBeInTheDocument();

    const cost = await screen.findByRole("region", { name: "Mon cost" });
    expect(within(cost).getByLabelText("Tier A")).toBeInTheDocument();
    expect(within(cost).getByText("4,4")).toBeInTheDocument();
    expect(within(cost).getByLabelText("Évolution du cost : +0,3")).toBeInTheDocument();

    const customs = screen.getByRole("region", { name: "Mes customs" });
    expect(await within(customs).findByText("38")).toBeInTheDocument();
    expect(within(customs).getByText("58 %")).toBeInTheDocument();
    expect(within(customs).getByText("71 / 100")).toBeInTheDocument();
    expect(within(customs).getByText("5 · 2")).toBeInTheDocument();
    expect(within(customs).getByRole("link", { name: /Voir ma fiche joueur/ })).toHaveAttribute("href", "/joueurs/1");

    const games = await screen.findByRole("region", { name: "Dernières games" });
    expect((await within(games).findAllByText("Lee Sin")).length).toBeGreaterThan(0);
    expect(within(games).getByText("86/100")).toBeInTheDocument();

    const ordre = [cost, customs, screen.getByRole("region", { name: "Joueur lié" })];
    for (let i = 1; i < ordre.length; i++) {
      expect(ordre[i - 1]!.compareDocumentPosition(ordre[i]!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    }
  });

  it("affiche « Superadmin » pour un Superadministrateur et aucun badge pour un utilisateur", async () => {
    mockApi(routes({ "GET /auth/me": user({ role: "Superadministrateur", isAdmin: true }) }));
    const { unmount } = renderApp("/compte");
    expect(await screen.findByText("Superadmin")).toBeInTheDocument();
    unmount();

    mockApi(routes({ "GET /auth/me": user() }));
    renderApp("/compte");
    await screen.findByRole("heading", { name: "Thomas" });
    expect(screen.queryByText("Admin")).not.toBeInTheDocument();
    expect(screen.queryByText("Superadmin")).not.toBeInTheDocument();
  });

  it("affiche la bannière Discord quand l'URL est fournie", async () => {
    mockApi(routes({ "GET /auth/me": user({ bannerUrl: "https://cdn.discordapp.com/banners/1/b.png" }) }));
    renderApp("/compte");

    expect(await screen.findByRole("img", { name: "Bannière Discord" })).toHaveAttribute(
      "src",
      "https://cdn.discordapp.com/banners/1/b.png",
    );
  });

  it("rafraîchit depuis Discord par une navigation complète, pas par un fetch", async () => {
    const assign = vi.fn();
    vi.stubGlobal("location", { ...window.location, assign });
    const fetchMock = mockApi(routes());
    renderApp("/compte");

    await userEvent.click(await screen.findByRole("button", { name: "Rafraîchir depuis Discord" }));

    expect(assign).toHaveBeenCalledWith("/api/auth/discord/refresh?returnUrl=%2Fcompte");
    expect(fetchMock.mock.calls.some(([r]) => new URL((r as Request).url).pathname.includes("/discord/refresh"))).toBe(false);
  });

  it("montre le Joueur lié avec la SoloQ, le rôle principal et le peak avec sa saison", async () => {
    mockApi(routes());
    renderApp("/compte");

    const joueur = await screen.findByRole("region", { name: "Joueur lié" });
    expect(within(joueur).getByText("Alice#EUW")).toBeInTheDocument();
    expect(within(joueur).getByText("Emerald II · 47 LP")).toBeInTheDocument();
    expect(within(joueur).getByText("Jungle")).toBeInTheDocument();
    expect(within(joueur).getByText("Diamond IV · 10 LP")).toBeInTheDocument();
    expect(within(joueur).getByText("S25")).toBeInTheDocument();
  });

  it("affiche les titres Roi/Reine et DOFD des games où le joueur les a remportés", async () => {
    mockApi(routes());
    renderApp("/compte");

    const games = await screen.findByRole("region", { name: "Dernières games" });
    const lignes = await within(games).findAllByRole("link");
    expect(within(lignes[0]!).getByText("Roi/Reine")).toBeInTheDocument();
    expect(within(lignes[0]!).queryByText("DOFD")).not.toBeInTheDocument();
    expect(within(lignes[1]!).getByText("DOFD")).toBeInTheDocument();
    expect(within(lignes[1]!).queryByText("Roi/Reine")).not.toBeInTheDocument();
    expect(within(lignes[2]!).queryByText("DOFD")).not.toBeInTheDocument();
    expect(within(lignes[2]!).queryByText("Roi/Reine")).not.toBeInTheDocument();
  });

  it("efface le peak après confirmation", async () => {
    let efface = false;
    mockApi(
      routes({
        "GET /players/me": () => json(efface ? { ...lie, peakTier: null, peakDivision: null, peakSeason: null, peakLp: 0 } : lie),
        "DELETE /players/peak": () => {
          efface = true;
          return json({ success: true });
        },
      }),
    );
    renderApp("/compte");

    await userEvent.click(await screen.findByRole("button", { name: "Effacer mon peak" }));
    const dialogue = await screen.findByRole("dialog");
    expect(dialogue).toHaveTextContent("Effacer mon peak ?");
    await userEvent.click(within(dialogue).getByRole("button", { name: "Effacer" }));

    await waitFor(() => expect(efface).toBe(true));
    await waitFor(() => expect(screen.queryByRole("button", { name: "Effacer mon peak" })).not.toBeInTheDocument());
  });

  it("n'efface pas le peak quand on annule la confirmation", async () => {
    let appele = false;
    mockApi(routes({ "DELETE /players/peak": () => { appele = true; return json({ success: true }); } }));
    renderApp("/compte");

    await userEvent.click(await screen.findByRole("button", { name: "Effacer mon peak" }));
    await userEvent.click(within(await screen.findByRole("dialog")).getByRole("button", { name: "Annuler" }));

    expect(appele).toBe(false);
    expect(screen.getByRole("button", { name: "Effacer mon peak" })).toBeInTheDocument();
  });

  it("règle le peak avec sa saison depuis le Profil", async () => {
    let body: unknown;
    mockApi(
      routes({
        "POST /players/peak": async (request) => {
          body = await request.json();
          return json({ ok: true });
        },
      }),
    );
    renderApp("/compte");

    await userEvent.click(await screen.findByRole("button", { name: "Régler mon peak" }));
    await userEvent.selectOptions(await screen.findByLabelText("Saison du peak"), "S24");
    await userEvent.click(screen.getByRole("button", { name: "Enregistrer le peak" }));

    await waitFor(() => expect(body).toMatchObject({ peakTier: "DIAMOND", peakDivision: 4, peakSeason: "S24" }));
  });

  it("délie le Joueur après confirmation, puis invite à lier son compte", async () => {
    let estLie = true;
    mockApi(
      routes({
        "GET /players/me": () => (estLie ? json(lie) : problem(404, "Aucun joueur n'est lié à ton compte Discord.")),
        "DELETE /players/link": () => {
          estLie = false;
          return json({ success: true });
        },
      }),
    );
    renderApp("/compte");

    await userEvent.click(await screen.findByRole("button", { name: "Délier" }));
    const dialogue = await screen.findByRole("dialog");
    expect(dialogue).toHaveTextContent("Le joueur et ses stats restent");
    await userEvent.click(within(dialogue).getByRole("button", { name: "Délier" }));

    expect(await screen.findByText("Aucun joueur n'est lié à ton compte Discord.")).toBeInTheDocument();
    expect(screen.getByRole("searchbox", { name: "Chercher un joueur" })).toBeInTheDocument();
    expect(screen.queryByRole("region", { name: "Mon cost" })).not.toBeInTheDocument();
  });

  it("change de joueur depuis le formulaire de liaison", async () => {
    let body: unknown;
    mockApi(
      routes({
        "POST /players/link": async (request) => {
          body = await request.json();
          return json({ success: true });
        },
      }),
    );
    renderApp("/compte");

    expect(screen.queryByRole("searchbox", { name: "Chercher un joueur" })).not.toBeInTheDocument();
    await userEvent.click(await screen.findByRole("button", { name: "Changer de joueur" }));
    await userEvent.click(await screen.findByRole("button", { name: "Lier Bob" }));

    await waitFor(() => expect(body).toEqual({ playerId: 2 }));
  });

  it("affiche « – » pour les données optionnelles absentes, jamais 0", async () => {
    mockApi(
      routes({
        "GET /players/me": player({ id: 1, currentCost: 3, costEvolution: null, mainRole: null, rankTier: null, lp: null }),
        "GET /players/1/fiche": fiche({
          stats: { ...stats, gamesJouees: 0, winrate: null, kda: null, noteMoyenne: null, participationAuxKills: null },
          games: [],
        }),
      }),
    );
    renderApp("/compte");

    const customs = await screen.findByRole("region", { name: "Mes customs" });
    expect((await within(customs).findAllByText("–")).length).toBeGreaterThanOrEqual(4);
    const joueur = screen.getByRole("region", { name: "Joueur lié" });
    expect(within(joueur).getAllByText("–").length).toBeGreaterThanOrEqual(3);
  });

  it("propose le thème et la déconnexion dans les préférences", async () => {
    mockApi(routes({ "POST /auth/logout": json({ success: true }) }));
    renderApp("/compte");

    const prefs = await screen.findByRole("region", { name: "Préférences" });
    await userEvent.click(within(prefs).getByRole("button", { name: "Clair" }));
    expect(document.documentElement).not.toHaveClass("dark");
    await userEvent.click(within(prefs).getByRole("button", { name: "Sombre" }));
    expect(document.documentElement).toHaveClass("dark");
    expect(within(prefs).getByRole("button", { name: "Se déconnecter" })).toBeInTheDocument();
  });
});
