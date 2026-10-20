import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockApi } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const players = [
  player({ id: 1, prenom: "Alice", rankTier: "GOLD", rankDivision: 4, lp: 0, currentCost: 2, costEvolution: -0.3 }),
  player({ id: 2, prenom: "Bob", rankTier: "GOLD", rankDivision: 2, lp: 0, currentCost: 3, costEvolution: 0 }),
  player({
    id: 3,
    prenom: "Chloé",
    rankTier: "SILVER",
    rankDivision: 1,
    lp: 0,
    peakTier: "PLATINUM",
    peakDivision: 3,
    peakLp: 0,
    peakSeason: "S24",
    currentCost: 6,
    costEvolution: 0.4,
  }),
  player({ id: 4, prenom: "Dan", rankTier: null, rankDivision: null, lp: null, currentCost: 1, costEvolution: null }),
  player({ id: 5, prenom: "Éva", rankTier: "GOLD", rankDivision: 3, lp: 0, currentCost: 4.5, costEvolution: 1.2 }),
];

describe("Classements, Master+", () => {
  it("keeps the Grandmaster peak above a Master current rank with more LP", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players": [
        player({
          id: 1,
          prenom: "Alice",
          rankTier: "MASTER",
          rankDivision: null,
          lp: 1700,
          peakTier: "GRANDMASTER",
          peakDivision: null,
          peakLp: 1100,
          peakSeason: "S25",
        }),
        player({ id: 2, prenom: "Bob", rankTier: "MASTER", rankDivision: null, lp: 1800 }),
      ],
    });
    renderApp("/classements?vue=soloq");

    const tiers = await screen.findAllByRole("region", { name: /^Tier / });
    expect(tiers.map((t) => t.getAttribute("aria-label"))).toEqual(["Tier Grandmaster", "Tier Master"]);
    expect(within(tiers[0]!).getByText("Peak S25")).toBeInTheDocument();
    expect(within(tiers[0]!).getByRole("link")).toHaveTextContent("Alice");
  });
});

describe("Classements", () => {
  beforeEach(() => {
    mockApi({ "GET /auth/me": user(), "GET /players": players });
  });

  it("opens on the cost ranking, with the tabs Cost then SoloQ", async () => {
    renderApp("/classements");

    const tabs = await screen.findAllByRole("tab");
    expect(tabs.map((t) => t.textContent)).toEqual(["Cost", "SoloQ", "DOFD"]);
    expect(screen.getByRole("tab", { name: "Cost", selected: true })).toBeInTheDocument();
  });

  it("shows the three best costs on a podium, then the others in a list", async () => {
    renderApp("/classements");

    const podium = await screen.findByRole("list", { name: "Podium du cost" });
    const places = within(podium).getAllByRole("listitem");
    // Ordre visuel : 2e, 1er, 3e.
    expect(places.map((p) => p.getAttribute("aria-label"))).toEqual(["2e : Éva", "1er : Chloé", "3e : Bob"]);
    expect(places[1]).toHaveTextContent("SS");
    expect(places[1]).toHaveTextContent("6,0");

    const liste = screen.getByRole("list", { name: "Classement du cost" });
    const lignes = within(liste).getAllByRole("listitem");
    expect(lignes.map((l) => l.getAttribute("aria-label"))).toEqual(["4e : Alice", "5e : Dan"]);
    expect(lignes[0]).toHaveTextContent("C");
    expect(lignes[0]).toHaveTextContent("2,0");
  });

  it("shows the evolution of the cost since last month, or nothing when unknown", async () => {
    renderApp("/classements");

    await screen.findByRole("list", { name: "Podium du cost" });
    expect(screen.getByLabelText("Évolution du cost : +0,4")).toHaveTextContent("▲ 0,4");
    expect(screen.getByLabelText("Évolution du cost : +1,2")).toHaveTextContent("▲ 1,2");
    expect(screen.getByLabelText("Évolution du cost : stable")).toHaveTextContent("= 0,0");
    expect(screen.getByLabelText("Évolution du cost : -0,3")).toHaveTextContent("▼ 0,3");
    expect(screen.getAllByLabelText(/^Évolution du cost/)).toHaveLength(4);
  });

  it("ranks the players by SoloQ tier, counting a better peak, with the level of the group", async () => {
    renderApp("/classements?vue=soloq");

    const tiers = await screen.findAllByRole("region", { name: /^Tier / });
    expect(tiers.map((t) => t.getAttribute("aria-label"))).toEqual(["Tier Platinum", "Tier Gold", "Tier Unranked"]);
    expect(within(tiers[0]!).getByText("Peak S24")).toBeInTheDocument();
    expect(within(tiers[1]!).getAllByRole("link").map((l) => l.textContent)).toEqual([
      expect.stringContaining("Bob"),
      expect.stringContaining("Éva"),
      expect.stringContaining("Alice"),
    ]);
    expect(screen.getByRole("group", { name: "Médiane" })).toHaveTextContent("Gold III · 50 LP");
  });

  it("switches to the SoloQ ranking, kept in the URL, and back", async () => {
    const { router } = renderApp("/classements");

    await userEvent.click(await screen.findByRole("tab", { name: "SoloQ" }));
    await waitFor(() => expect(router.state.location.search).toEqual({ vue: "soloq" }));
    expect(await screen.findAllByRole("region", { name: /^Tier / })).not.toHaveLength(0);

    await userEvent.click(screen.getByRole("tab", { name: "Cost" }));
    await waitFor(() => expect(router.state.location.search).toEqual({}));
    expect(await screen.findByRole("list", { name: "Podium du cost" })).toBeInTheDocument();
  });
});

describe("Classement des costs, deux modes", () => {
  beforeEach(() => {
    localStorage.clear();
    mockApi({ "GET /auth/me": user(), "GET /players": players });
  });

  it("shows the six steps from D to SS, each player standing on the step of their tier", async () => {
    renderApp("/classements");
    await userEvent.click(await screen.findByRole("radio", { name: "Escalier" }));

    const escalier = await screen.findByRole("list", { name: "Escalier du cost" });
    expect(screen.queryByRole("list", { name: "Podium du cost" })).not.toBeInTheDocument();
    const marches = within(escalier).getAllByRole("listitem", { name: /^Marche / });
    expect(marches.map((m) => m.getAttribute("aria-label"))).toEqual(["Marche D", "Marche C", "Marche B", "Marche A", "Marche S", "Marche SS"]);
    const prenoms = marches.map((m) => within(m).queryAllByRole("link").map((l) => l.textContent));
    expect(prenoms).toEqual([["Dan"], ["Alice"], ["Bob"], [], ["Éva"], ["Chloé"]]);
  });

  it("does not rank the players inside a tier: alphabetical by first name", async () => {
    mockApi({
      "GET /auth/me": user(),
      "GET /players": [
        player({ id: 1, prenom: "Zoé", currentCost: 6 }),
        player({ id: 2, prenom: "Ann", currentCost: 5.6 }),
        player({ id: 3, prenom: "Léo", currentCost: 5.8 }),
      ],
    });
    renderApp("/classements");
    await userEvent.click(await screen.findByRole("radio", { name: "Escalier" }));

    const marche = await screen.findByRole("listitem", { name: "Marche SS" });
    expect(within(marche).getAllByRole("link").map((l) => l.textContent)).toEqual(["Ann", "Léo", "Zoé"]);
  });

  it("shows the cost and the month trend of a player on hover", async () => {
    renderApp("/classements");
    await userEvent.click(await screen.findByRole("radio", { name: "Escalier" }));

    const marche = await screen.findByRole("listitem", { name: "Marche SS" });
    expect(within(marche).queryByRole("tooltip")).not.toBeInTheDocument();
    await userEvent.hover(within(marche).getByRole("link", { name: "Chloé" }));
    const info = within(marche).getByRole("tooltip");
    expect(info).toHaveTextContent("6,0");
    expect(within(info).getByLabelText("Évolution du cost : +0,4")).toBeInTheDocument();
    await userEvent.unhover(within(marche).getByRole("link", { name: "Chloé" }));
    expect(within(marche).queryByRole("tooltip")).not.toBeInTheDocument();
  });

  it("keeps the chosen mode for the user", async () => {
    const premier = renderApp("/classements");
    await userEvent.click(await screen.findByRole("radio", { name: "Escalier" }));
    premier.unmount();

    renderApp("/classements");
    expect(await screen.findByRole("list", { name: "Escalier du cost" })).toBeInTheDocument();
    expect(screen.getByRole("radio", { name: "Escalier", checked: true })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("radio", { name: "Podium et liste" }));
    expect(await screen.findByRole("list", { name: "Podium du cost" })).toBeInTheDocument();
  });

  it("still switches mode when the browser refuses storage", async () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new Error("denied");
    });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new Error("denied");
    });
    renderApp("/classements");
    await userEvent.click(await screen.findByRole("radio", { name: "Escalier" }));
    expect(await screen.findByRole("list", { name: "Escalier du cost" })).toBeInTheDocument();
    vi.restoreAllMocks();
  });

  it("leaves the SoloQ view without the mode choice", async () => {
    renderApp("/classements?vue=soloq");
    await screen.findAllByRole("region", { name: /^Tier / });
    expect(screen.queryByRole("radio", { name: "Escalier" })).not.toBeInTheDocument();
  });
});
