import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { mockApi } from "@/test/api";
import { player, user } from "@/test/fixtures";
import { renderApp } from "@/test/render";

const players = [
  player({ id: 1, prenom: "Alice", rankTier: "GOLD", rankDivision: 4, lp: 0, currentCost: 2 }),
  player({ id: 2, prenom: "Bob", rankTier: "GOLD", rankDivision: 2, lp: 0, currentCost: 3 }),
  player({
    id: 3,
    prenom: "Chloé",
    rankTier: "SILVER",
    rankDivision: 1,
    lp: 0,
    peakTier: "PLATINUM",
    peakDivision: 3,
    peakLp: 0,
    peakSeason: "S2024",
    currentCost: 6,
  }),
  player({ id: 4, prenom: "Dan", rankTier: null, rankDivision: null, lp: null, currentCost: 1 }),
];

describe("Classements", () => {
  beforeEach(() => {
    mockApi({ "GET /auth/me": user(), "GET /players": players });
  });

  it("ranks the players by SoloQ tier, counting a better peak, with the level of the group", async () => {
    renderApp("/classements");

    const tiers = await screen.findAllByRole("region", { name: /^Tier / });
    expect(tiers.map((t) => t.getAttribute("aria-label"))).toEqual(["Tier Platinum", "Tier Gold", "Tier Unranked"]);
    expect(within(tiers[0]!).getByText("Peak S2024")).toBeInTheDocument();
    expect(within(tiers[1]!).getAllByRole("link").map((l) => l.textContent)).toEqual([
      expect.stringContaining("Bob"),
      expect.stringContaining("Alice"),
    ]);
    expect(screen.getByRole("group", { name: "Médiane" })).toHaveTextContent("Gold II · 0 LP");
    expect(screen.getByRole("group", { name: "Moyenne" })).toHaveTextContent("Gold II · 33 LP");
  });

  it("switches to the custom ranking by cost, kept in the URL", async () => {
    const { router } = renderApp("/classements");

    await userEvent.click(await screen.findByRole("tab", { name: "Cost" }));

    await waitFor(() => expect(router.state.location.search).toEqual({ vue: "cost" }));
    const tiers = await screen.findAllByRole("region", { name: /^Tier / });
    expect(tiers.map((t) => t.getAttribute("aria-label"))).toEqual(["Tier SS", "Tier B", "Tier C", "Tier D"]);
    expect(screen.getByRole("group", { name: "Cost moyen" })).toHaveTextContent("3.0 pts");
  });

  it("opens directly on the custom ranking", async () => {
    renderApp("/classements?vue=cost");

    expect(await screen.findByRole("tab", { name: "Cost", selected: true })).toBeInTheDocument();
  });
});
