import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { json, mockApi, problem } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const ligne = (playerId: number, prenom: string, winrate: number | null, gamesJouees = 10) => ({
  playerId,
  prenom,
  riotId: `${prenom}#EUW`,
  gamesJouees,
  victoires: 0,
  defaites: 0,
  winrate,
});

const rowNames = () =>
  screen
    .getAllByRole("row")
    .slice(1)
    .map((row) => within(row).getAllByRole("link")[0]!.textContent!.slice(0, 3));

describe("Joueurs", () => {
  it("lists the players in a table sorted by cost, with cost, tier letter, role, ranks and winrate", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players": [
        player({ id: 1, prenom: "Alice", riotId: "Alice#EUW", currentCost: 3, mainRole: "TOP", rankTier: "GOLD", rankDivision: 2, lp: 40 }),
        player({
          id: 2,
          prenom: "Bob",
          riotId: "Bob#EUW",
          currentCost: 6,
          mainRole: "JUNGLE",
          peakTier: "DIAMOND",
          peakDivision: 1,
          peakLp: 0,
        }),
        player({ id: 3, prenom: "Chloé", riotId: "Chloé#EUW", currentCost: 3.4, mainRole: "BOTTOM" }),
      ],
      "GET /classements/5v5": { joueurs: [ligne(1, "Alice", 68.4), ligne(2, "Bob", null, 0)] },
    });

    renderApp("/joueurs");

    expect(await screen.findByRole("table")).toBeInTheDocument();
    await screen.findByText("68 %");
    expect(rowNames()).toEqual(["Bob", "Chl", "Ali"]);
    expect(screen.getByRole("columnheader", { name: /Cost/ })).toHaveAttribute("aria-sort", "descending");

    const alice = screen.getByRole("row", { name: /Alice/ });
    expect(within(alice).getByRole("link", { name: /Alice/ })).toHaveAttribute("href", "/joueurs/1");
    expect(within(alice).getByText("B")).toBeInTheDocument();
    expect(within(alice).getByText("3")).toBeInTheDocument();
    expect(within(alice).getByText("Top")).toBeInTheDocument();
    expect(within(alice).getByText("Gold II · 40 LP")).toBeInTheDocument();
    expect(within(alice).getByText("non renseigné")).toBeInTheDocument();

    const bob = screen.getByRole("row", { name: /Bob/ });
    expect(within(bob).getByText("SS")).toBeInTheDocument();
    expect(within(bob).getByText(/Diamond I/)).toBeInTheDocument();
    expect(within(bob).getByText("—")).toBeInTheDocument();
  });

  it("sorts by the clicked column and reverses on a second click", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players": [
        player({ id: 1, prenom: "Alice", riotId: "Alice#EUW", currentCost: 3 }),
        player({ id: 2, prenom: "Bob", riotId: "Bob#EUW", currentCost: 6 }),
        player({ id: 3, prenom: "Chloé", riotId: "Chloé#EUW", currentCost: 1 }),
      ],
      "GET /classements/5v5": { joueurs: [] },
    });

    renderApp("/joueurs");
    await screen.findByRole("table");

    await userEvent.click(screen.getByRole("button", { name: "Joueur" }));
    expect(rowNames()).toEqual(["Ali", "Bob", "Chl"]);
    expect(screen.getByRole("columnheader", { name: /Joueur/ })).toHaveAttribute("aria-sort", "ascending");

    await userEvent.click(screen.getByRole("button", { name: "Joueur" }));
    expect(rowNames()).toEqual(["Chl", "Bob", "Ali"]);
  });

  it("filters by name or by role", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players": [
        player({ id: 1, prenom: "Alice", riotId: "Alice#EUW", mainRole: "TOP" }),
        player({ id: 2, prenom: "Bob", riotId: "Bob#EUW", mainRole: "BOTTOM" }),
      ],
      "GET /classements/5v5": { joueurs: [] },
    });

    renderApp("/joueurs");
    const search = await screen.findByRole("searchbox", { name: "Chercher un joueur" });

    await userEvent.type(search, "bob");
    expect(screen.queryByRole("row", { name: /Alice/ })).not.toBeInTheDocument();
    expect(screen.getByRole("row", { name: /Bob/ })).toBeInTheDocument();

    await userEvent.clear(search);
    await userEvent.type(search, "top");
    expect(screen.getByRole("row", { name: /Alice/ })).toBeInTheDocument();
    expect(screen.queryByRole("row", { name: /Bob/ })).not.toBeInTheDocument();

    await userEvent.clear(search);
    await userEvent.type(search, "zzz");
    expect(screen.getByText("Aucun joueur ne correspond.")).toBeInTheDocument();
  });

  it("still lists the players when the customs winrates are unavailable", async () => {
    mockApi({ "GET /auth/me": user(), "GET /players": [player({ id: 1, prenom: "Alice" })] });

    renderApp("/joueurs");

    expect(await screen.findByRole("row", { name: /Alice/ })).toBeInTheDocument();
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
        peakSeason: "S24",
        currentCost: 4,
        mainRole: "JUNGLE",
      }),
    });

    renderApp("/joueurs/7");

    expect(await screen.findByRole("heading", { name: "Kélio" })).toBeInTheDocument();
    expect(screen.getByText("Kelio#EUW")).toBeInTheDocument();
    expect(screen.getByText("Emerald I · 75 LP")).toBeInTheDocument();
    expect(screen.getByText("Diamond II · 10 LP")).toBeInTheDocument();
    expect(screen.getByText("S24")).toBeInTheDocument();
    expect(screen.getByText("A")).toBeInTheDocument();
    expect(screen.getByText("4 pts")).toBeInTheDocument();
    expect(screen.getByText("Jungle")).toBeInTheDocument();
  });

  it("says when a player does not exist", async () => {
    mockApi({ "GET /auth/me": user(), "GET /players/99": problem(404) });

    renderApp("/joueurs/99");

    expect(await screen.findByRole("alert")).toHaveTextContent("Introuvable.");
  });

  it("previews the Riot account in a dialog before saving the new player", async () => {
    let created: unknown;
    const fetch = mockApi({
      "GET /auth/me": user(),
      "GET /players": [player()],
      "GET /classements/5v5": { joueurs: [] },
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

    const { router } = renderApp("/joueurs");

    await userEvent.click(await screen.findByRole("button", { name: "Ajouter un joueur" }));
    const dialog = await screen.findByRole("dialog", { name: "Ajouter un joueur" });
    await userEvent.type(within(dialog).getByLabelText("Prénom"), "Lee");
    await userEvent.type(within(dialog).getByLabelText("RiotId"), "Faker#KR1");
    await userEvent.click(within(dialog).getByRole("button", { name: "Vérifier le compte" }));

    const preview = await within(dialog).findByRole("region", { name: "Compte Riot trouvé" });
    expect(within(preview).getByText("Challenger · 1200 LP")).toBeInTheDocument();
    expect(within(preview).getByText("Mid")).toBeInTheDocument();
    expect(fetch.mock.calls.some(([r]) => r.method === "POST")).toBe(false);

    await userEvent.click(within(dialog).getByRole("button", { name: "Ajouter Lee" }));

    expect(created).toEqual({ prenom: "Lee", riotId: "Faker#KR1" });
    await waitFor(() => expect(router.state.location.pathname).toBe("/joueurs/12"));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("does not save an account that is already a player, nor an unknown account", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players": [],
      "GET /classements/5v5": { joueurs: [] },
      "GET /players/riot-preview": (request) =>
        new URL(request.url).searchParams.get("riotId") === "Deja#EUW"
          ? json({ riotId: "Deja#EUW", rankTier: null, rankDivision: null, lp: null, mainRole: null, alreadyRegistered: true })
          : problem(404, "Compte Riot introuvable."),
    });

    renderApp("/joueurs");

    await userEvent.click(await screen.findByRole("button", { name: "Ajouter un joueur" }));
    const dialog = await screen.findByRole("dialog");
    await userEvent.type(within(dialog).getByLabelText("Prénom"), "Déjà");
    await userEvent.type(within(dialog).getByLabelText("RiotId"), "Deja#EUW");
    await userEvent.click(within(dialog).getByRole("button", { name: "Vérifier le compte" }));

    expect(await within(dialog).findByText("Ce compte Riot est déjà un Joueur.")).toBeInTheDocument();
    expect(within(dialog).getByRole("button", { name: "Ajouter Déjà" })).toBeDisabled();

    await userEvent.clear(within(dialog).getByLabelText("RiotId"));
    await userEvent.type(within(dialog).getByLabelText("RiotId"), "Personne#EUW");
    await userEvent.click(within(dialog).getByRole("button", { name: "Vérifier le compte" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent("Compte Riot introuvable.");
    expect(within(dialog).queryByRole("button", { name: "Ajouter Déjà" })).not.toBeInTheDocument();

    await userEvent.click(within(dialog).getByRole("button", { name: "Annuler" }));
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });
});
