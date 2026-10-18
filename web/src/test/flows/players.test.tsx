import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { json, mockApi, problem } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

describe("Joueurs", () => {
  it("groups the players by cost tier, from SS down", async () => {
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

    const tiers = await screen.findAllByRole("region", { name: /^Tier / });
    expect(tiers.map((t) => t.getAttribute("aria-label"))).toEqual(["Tier SS", "Tier B", "Tier D"]);
    expect(within(tiers[1]!).getAllByRole("link").map((l) => l.textContent)).toEqual([
      expect.stringContaining("Chloé"),
      expect.stringContaining("Alice"),
    ]);
    expect(within(tiers[0]!).getByRole("link", { name: /Bob/ })).toHaveAttribute("href", "/joueurs/2");
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
