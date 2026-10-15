import { COST_TIERS, costTier, groupByCostTier, points } from "./cost";

describe("points", () => {
  it("shows a cost in points, with at most two decimals", () => {
    expect(points(3)).toBe("3 pts");
    expect(points(2.5)).toBe("2.5 pts");
    expect(points(1.3333)).toBe("1.33 pts");
  });
});

describe("costTier", () => {
  it("maps each cost from 1 to 6 to its tier", () => {
    expect([1, 2, 3, 4, 5, 6].map(costTier)).toEqual(["D", "C", "B", "A", "S", "SS"]);
  });

  it("rounds a fractional cost set by an admin", () => {
    expect(costTier(3.4)).toBe("B");
    expect(costTier(4.5)).toBe("S");
  });

  it("puts a player without cost yet in D", () => {
    expect(costTier(0)).toBe("D");
  });

  it("lists the tiers from the highest", () => {
    expect(COST_TIERS).toEqual(["SS", "S", "A", "B", "C", "D"]);
  });
});

describe("groupByCostTier", () => {
  it("keeps the non-empty tiers from SS down, highest cost first within a tier", () => {
    const players = [
      { prenom: "Alice", currentCost: 3 },
      { prenom: "Bob", currentCost: 6 },
      { prenom: "Chloé", currentCost: 3.4 },
      { prenom: "Anna", currentCost: 3 },
    ];

    expect(groupByCostTier(players)).toEqual([
      { tier: "SS", players: [{ prenom: "Bob", currentCost: 6 }] },
      {
        tier: "B",
        players: [
          { prenom: "Chloé", currentCost: 3.4 },
          { prenom: "Alice", currentCost: 3 },
          { prenom: "Anna", currentCost: 3 },
        ],
      },
    ]);
  });
});
