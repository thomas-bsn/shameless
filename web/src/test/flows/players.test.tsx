import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { json, mockApi, problem } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

describe("Joueurs", () => {
  it("lists players in a searchable table and sorts by cost", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players": [
        player({ id: 1, prenom: "Alice", currentCost: 3 }),
        player({ id: 2, prenom: "Bob", currentCost: 6 }),
        player({ id: 3, prenom: "Chloé", currentCost: 3.4 }),
        player({ id: 4, prenom: "Dan", currentCost: 1 }),
      ],
    });

    renderApp("/joueurs");

    const table = await screen.findByRole("table");
    const rows = () => within(table).getAllByRole("row").slice(1).map((row) => row.textContent);
    expect(rows()).toHaveLength(4);
    expect(rows()[0]).toContain("Alice");
    await userEvent.click(screen.getByRole("button", { name: "Cost" }));
    expect(rows()[0]).toContain("Bob");
    expect(within(table).getByRole("link", { name: /Bob/ })).toHaveAttribute("href", "/joueurs/2");
    await userEvent.type(screen.getByRole("textbox", { name: "Rechercher un joueur" }), "Chloé");
    expect(rows()).toHaveLength(1);
    expect(rows()[0]).toContain("Chloé");
  });

  it("filters the table by the player's main role", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players": [player({ id: 1, prenom: "Alice", mainRole: "MIDDLE" }), player({ id: 2, prenom: "Bob", mainRole: "JUNGLE" })],
    });
    renderApp("/joueurs");
    const table = await screen.findByRole("table");
    await userEvent.type(screen.getByRole("textbox", { name: "Rechercher un joueur" }), "jungle");
    expect(within(table).getAllByRole("row")).toHaveLength(2);
    expect(within(table).getByRole("link", { name: /Bob/ })).toBeInTheDocument();
  });

  it("keeps each letter visible for all six cost tiers", async () => {
    mockApi({ "GET /auth/me": user(), "GET /players": [1, 2, 3, 4, 5, 6].map((cost, i) => player({ id: i + 1, prenom: `J${i + 1}`, currentCost: cost })) });
    renderApp("/joueurs");
    const table = await screen.findByRole("table");
    for (const tier of ["D", "C", "B", "A", "S", "SS"]) expect(within(table).getByText(tier)).toBeVisible();
  });

  it("shows custom winrate from the 5v5 ranking and sorts that column", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players": [player({ id: 1, prenom: "Alice" }), player({ id: 2, prenom: "Bob" })],
      "GET /classements/5v5": { joueurs: [
        { playerId: 1, prenom: "Alice", riotId: "Alice#EUW", gamesJouees: 4, victoires: 1, defaites: 3, winrate: 25 },
        { playerId: 2, prenom: "Bob", riotId: "Bob#EUW", gamesJouees: 2, victoires: 2, defaites: 0, winrate: 100 },
      ] },
    });
    renderApp("/joueurs");
    const table = await screen.findByRole("table");
    expect(within(table).getByText("25%")).toBeInTheDocument();
    expect(within(table).getByText("100%")).toBeInTheDocument();
    await userEvent.click(within(table).getByRole("button", { name: "Winrate customs" }));
    expect(within(table).getAllByRole("row")[1]).toHaveTextContent("Bob");
  });

  it("shows all player profile sections and keeps missing statistics empty", async () => {
    const profile = {
      playerId: 7, prenom: "Kélio", riotId: "Kelio#EUW", discord: { discordId: "900", username: "kelio", avatarUrl: null }, cost: 4,
      soloQ: null, flex: { tier: "GOLD", division: 2, lp: 40 },
      stats: { gamesJouees: 1, victoires: 1, defaites: 0, winrate: 100, kda: null, killsMoyens: null, mortsMoyennes: null, assistsMoyens: null, csParMinute: null, participationAuxKills: null, degatsMoyens: null, degatsParMinute: null, noteMoyenne: null, gamesNotees: 0 },
      champions: [{ championId: "Ahri", stats: { gamesJouees: 1, victoires: 1, defaites: 0, winrate: 100, kda: null, killsMoyens: null, mortsMoyennes: null, assistsMoyens: null, csParMinute: null, participationAuxKills: null, degatsMoyens: null, degatsParMinute: null, noteMoyenne: null, gamesNotees: 0 } }],
      roles: [{ role: "Mid", stats: { gamesJouees: 1, victoires: 1, defaites: 0, winrate: 100, kda: null, killsMoyens: null, mortsMoyennes: null, assistsMoyens: null, csParMinute: null, participationAuxKills: null, degatsMoyens: null, degatsParMinute: null, noteMoyenne: null, gamesNotees: 0 } }],
      games: [{ gameId: 11, seriesId: 5, playedAt: "2026-10-03T20:00:00Z", equipe: "A", victoire: true, dureeSecondes: null, championId: "Ahri", role: "Mid", stats: null, performance: { note: 83, role: "Mid", sourceDuRole: "Rapport", composantes: [] } }],
      titres: { mvp: 2, dofd: 1 },
    };
    mockApi({
      "GET /auth/me": user(), "GET /players/7": player({ id: 7, prenom: "Kélio", riotId: "Kelio#EUW", flexRankTier: "GOLD", flexRankDivision: 2, flexLp: 40, currentCost: 4 }),
      "GET /players/7/fiche": profile, "GET /players/7/cost/historique": { playerId: 7, mois: [{ mois: "2026-09-01", cost: 3, costDeDepart: 2, gamesTerminees: 5, victoires: 3, noteMoyenne: 70, evolution: null }, { mois: "2026-10-01", cost: 4, costDeDepart: 3, gamesTerminees: 5, victoires: 3, noteMoyenne: 75, evolution: 1 }] },
    });
    renderApp("/joueurs/7");
    expect(await screen.findByRole("heading", { name: "Kélio" })).toBeInTheDocument();
    expect(screen.getAllByText("Gold II · 40 LP")).toHaveLength(2);
    expect(screen.getByText("Discord · kelio")).toBeInTheDocument();
    expect(screen.getByText("2 MVP")).toBeInTheDocument();
    expect(screen.getAllByText(/100%/)).not.toHaveLength(0);
    expect(screen.getByRole("img", { name: "Évolution mensuelle du cost de 1 à 6" })).toBeInTheDocument();
    expect(screen.getAllByText("Ahri")).toHaveLength(2);
    expect(screen.getByText("83/100")).toBeInTheDocument();
    expect(screen.getByText("KDA").parentElement?.parentElement).toHaveTextContent("—");
  });

  it("shows a player's Riot account, SoloQ rank, peak, cost and main role", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players/7": player({
        id: 7,
        prenom: "Kélio",
        riotId: "Kelio#EUW",
        rankTier: "EMERALD",
        rankDivision: 1,
        lp: 75,
        peakTier: "DIAMOND",
        peakDivision: 2,
        peakLp: 10,
        peakSeason: "S2024",
        currentCost: 4,
        mainRole: "JUNGLE",
      }),
    });

    renderApp("/joueurs/7");

    expect(await screen.findByRole("heading", { name: "Kélio" })).toBeInTheDocument();
    expect(screen.getByText("Kelio#EUW")).toBeInTheDocument();
    expect(screen.getByText("Emerald I · 75 LP")).toBeInTheDocument();
    expect(screen.getByText("Diamond II · 10 LP")).toBeInTheDocument();
    expect(screen.getByText("S2024")).toBeInTheDocument();
    expect(screen.getByText("A")).toBeInTheDocument();
    expect(screen.getByText("4 pts")).toBeInTheDocument();
    expect(screen.getByText("Jungle")).toBeInTheDocument();
  });

  it("says when a player does not exist", async () => {
    mockApi({ "GET /auth/me": user(), "GET /players/99": problem(404) });

    renderApp("/joueurs/99");

    expect(await screen.findByRole("alert")).toHaveTextContent("Introuvable.");
  });

  it("previews the Riot account before saving the new player", async () => {
    let created: unknown;
    const fetch = mockApi({
      "GET /auth/me": user(),
      "GET /players/riot-preview": (request) =>
        new URL(request.url).searchParams.get("riotId") === "Faker#KR1"
          ? json({
              riotId: "Faker#KR1",
              rankTier: "CHALLENGER",
              rankDivision: null,
              lp: 1200,
              mainRole: "MIDDLE",
              alreadyRegistered: false,
            })
          : problem(404, "Compte Riot introuvable."),
      "POST /players": async (request) => {
        created = await request.json();
        return json({ id: 12, prenom: "Lee", riotId: "Faker#KR1", rankTier: "CHALLENGER", rankDivision: null, lp: 1200 });
      },
      "GET /players/12": player({ id: 12, prenom: "Lee", riotId: "Faker#KR1" }),
    });

    const { router } = renderApp("/joueurs/nouveau");

    await userEvent.type(await screen.findByLabelText("Prénom"), "Lee");
    await userEvent.type(screen.getByLabelText("RiotId"), "Faker#KR1");
    await userEvent.click(screen.getByRole("button", { name: "Prévisualiser" }));

    const preview = await screen.findByRole("region", { name: "Compte Riot" });
    expect(within(preview).getByText("Challenger · 1200 LP")).toBeInTheDocument();
    expect(within(preview).getByText("Mid")).toBeInTheDocument();
    expect(fetch.mock.calls.some(([r]) => r.method === "POST")).toBe(false);

    await userEvent.click(screen.getByRole("button", { name: "Enregistrer" }));

    expect(created).toEqual({ prenom: "Lee", riotId: "Faker#KR1" });
    await waitFor(() => expect(router.state.location.pathname).toBe("/joueurs/12"));
  });

  it("does not save an account that is already a player, nor an unknown account", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players/riot-preview": (request) =>
        new URL(request.url).searchParams.get("riotId") === "Deja#EUW"
          ? json({ riotId: "Deja#EUW", rankTier: null, rankDivision: null, lp: null, mainRole: null, alreadyRegistered: true })
          : problem(404, "Compte Riot introuvable."),
    });

    renderApp("/joueurs/nouveau");

    await userEvent.type(await screen.findByLabelText("Prénom"), "Déjà");
    await userEvent.type(screen.getByLabelText("RiotId"), "Deja#EUW");
    await userEvent.click(screen.getByRole("button", { name: "Prévisualiser" }));

    expect(await screen.findByText("Ce compte Riot est déjà un Joueur.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Enregistrer" })).toBeDisabled();

    await userEvent.clear(screen.getByLabelText("RiotId"));
    await userEvent.type(screen.getByLabelText("RiotId"), "Personne#EUW");
    await userEvent.click(screen.getByRole("button", { name: "Prévisualiser" }));

    expect(await screen.findByRole("alert")).toHaveTextContent("Compte Riot introuvable.");
    expect(screen.queryByRole("button", { name: "Enregistrer" })).not.toBeInTheDocument();
  });
});
