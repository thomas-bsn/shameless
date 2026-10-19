import { averageCost, compareRanks, displayRank, groupBySoloQTier, rankScore, scoreToRank, soloQLevel } from "./soloq";

const rank = (
  rankTier: string | null,
  rankDivision: number | null,
  lp: number | null,
  peak: { peakTier?: string | null; peakDivision?: number | null; peakLp?: number } = {},
) => ({ rankTier, rankDivision, lp, peakTier: null, peakDivision: null, peakLp: 0, ...peak });

describe("rankScore", () => {
  it("counts 400 per tier, 100 per division and the LP", () => {
    expect(rankScore("IRON", 4, 0)).toBe(0);
    expect(rankScore("GOLD", 2, 40)).toBe(3 * 400 + 200 + 40);
  });

  it("has no division from Master up", () => {
    expect(rankScore("MASTER", null, 250)).toBe(7 * 400 + 250);
  });
});

describe("scoreToRank", () => {
  it("is the inverse of rankScore", () => {
    expect(scoreToRank(rankScore("GOLD", 2, 40))).toEqual({ tier: "GOLD", division: 2, lp: 40 });
    expect(scoreToRank(rankScore("MASTER", null, 250))).toEqual({ tier: "MASTER", division: null, lp: 250 });
  });

  it("rounds the LP of an average", () => {
    expect(scoreToRank(1440.6)).toEqual({ tier: "GOLD", division: 2, lp: 41 });
  });
});

describe("displayRank", () => {
  it("shows the peak when it is above the current rank", () => {
    expect(displayRank(rank("GOLD", 2, 40, { peakTier: "DIAMOND", peakDivision: 4, peakLp: 0 }))).toEqual({
      tier: "DIAMOND",
      division: 4,
      lp: 0,
      isPeak: true,
    });
  });

  it("compares divisions and LP within the same tier", () => {
    expect(displayRank(rank("DIAMOND", 1, 50, { peakTier: "DIAMOND", peakDivision: 3, peakLp: 20 })).isPeak).toBe(false);
    expect(displayRank(rank("DIAMOND", 3, 50, { peakTier: "DIAMOND", peakDivision: 1, peakLp: 0 })).isPeak).toBe(true);
  });

  it("shows the current rank when there is no peak", () => {
    expect(displayRank(rank("SILVER", 1, 99))).toEqual({ tier: "SILVER", division: 1, lp: 99, isPeak: false });
  });

  it("falls back to the peak of an unranked player, or Unranked", () => {
    expect(displayRank(rank("UNRANKED", null, 0, { peakTier: "GOLD", peakDivision: 1, peakLp: 0 })).tier).toBe("GOLD");
    expect(displayRank(rank(null, null, null))).toEqual({ tier: "UNRANKED", division: null, lp: null, isPeak: false });
  });
});

describe("groupBySoloQTier", () => {
  it("keeps the non-empty tiers from Challenger down, best rank first", () => {
    const groups = groupBySoloQTier([
      { prenom: "A", ...rank("GOLD", 3, 0) },
      { prenom: "B", ...rank("GOLD", 1, 10) },
      { prenom: "C", ...rank(null, null, null) },
      { prenom: "D", ...rank("SILVER", 1, 0, { peakTier: "MASTER", peakLp: 10 }) },
    ]);

    expect(groups.map((g) => [g.tier, g.players.map((p) => p.prenom)])).toEqual([
      ["MASTER", ["D"]],
      ["GOLD", ["B", "A"]],
      ["UNRANKED", ["C"]],
    ]);
  });
});

describe("groupBySoloQTier, Master+", () => {
  it("orders by tier before LP inside the Master+ groups and shows the peak tier", () => {
    const groups = groupBySoloQTier([
      { prenom: "A", ...rank("MASTER", null, 1700) },
      { prenom: "B", ...rank("MASTER", null, 1700, { peakTier: "GRANDMASTER", peakLp: 1100 }) },
      { prenom: "C", ...rank("GRANDMASTER", null, 900) },
    ]);
    expect(groups.map((g) => [g.tier, g.players.map((p) => p.prenom)])).toEqual([
      ["GRANDMASTER", ["B", "C"]],
      ["MASTER", ["A"]],
    ]);
  });
});

describe("soloQLevel", () => {
  it("gives the median and average rank of the ranked players", () => {
    const level = soloQLevel([rank("GOLD", 4, 0), rank("GOLD", 2, 0), rank("PLATINUM", 3, 0), rank(null, null, null)]);

    expect(level).toEqual({
      median: { tier: "GOLD", division: 2, lp: 0 },
      average: { tier: "GOLD", division: 2, lp: 33 },
    });
  });

  it("averages the two middle players of an even count", () => {
    expect(soloQLevel([rank("GOLD", 4, 0), rank("GOLD", 3, 0)])?.median).toEqual({ tier: "GOLD", division: 4, lp: 50 });
  });

  it("is null without any ranked player", () => {
    expect(soloQLevel([rank(null, null, null)])).toBeNull();
  });
});

describe("averageCost", () => {
  it("averages the current costs", () => {
    expect(averageCost([{ currentCost: 2 }, { currentCost: 5 }])).toBe(3.5);
    expect(averageCost([])).toBeNull();
  });
});

describe("compareRanks", () => {
  const r = (tier: string | null, division: number | null, lp: number | null) => ({ tier, division, lp });

  it("a higher tier always wins, whatever the LP (Master+ thresholds change each season)", () => {
    expect(compareRanks(r("GRANDMASTER", null, 1100), r("MASTER", null, 1700))).toBeGreaterThan(0);
    expect(compareRanks(r("MASTER", null, 1700), r("GRANDMASTER", null, 1100))).toBeLessThan(0);
    expect(compareRanks(r("CHALLENGER", null, 0), r("GRANDMASTER", null, 5000))).toBeGreaterThan(0);
    expect(compareRanks(r("MASTER", null, 0), r("DIAMOND", 1, 99))).toBeGreaterThan(0);
  });

  it("compares the division inside a tier, I above IV, then the LP", () => {
    expect(compareRanks(r("GOLD", 1, 0), r("GOLD", 2, 99))).toBeGreaterThan(0);
    expect(compareRanks(r("GOLD", 2, 50), r("GOLD", 2, 40))).toBeGreaterThan(0);
    expect(compareRanks(r("MASTER", null, 300), r("MASTER", null, 500))).toBeLessThan(0);
    expect(compareRanks(r("GOLD", 2, 40), r("GOLD", 2, 40))).toBe(0);
  });

  it("puts unranked below everything and treats unknown LP as 0", () => {
    expect(compareRanks(r(null, null, null), r("IRON", 4, 0))).toBeLessThan(0);
    expect(compareRanks(r("UNRANKED", null, 0), r(null, null, null))).toBe(0);
    expect(compareRanks(r("GOLD", 2, null), r("GOLD", 2, 0))).toBe(0);
  });
});

describe("Master+ in the average level", () => {
  it("never leaves the tier of the LP, however many LP", () => {
    expect(scoreToRank(rankScore("MASTER", null, 1700)).tier).toBe("MASTER");
    expect(scoreToRank(rankScore("GRANDMASTER", null, 1100)).tier).toBe("GRANDMASTER");
    expect(scoreToRank(rankScore("CHALLENGER", null, 3000)).tier).toBe("CHALLENGER");
  });
});

describe("displayRank, tier before LP", () => {
  it("shows a Grandmaster peak over a Master current rank with more LP", () => {
    expect(displayRank(rank("MASTER", null, 1700, { peakTier: "GRANDMASTER", peakLp: 1100 }))).toEqual({
      tier: "GRANDMASTER",
      division: null,
      lp: 1100,
      isPeak: true,
    });
  });

  it("keeps the current rank when it is above the peak in the same tier", () => {
    expect(displayRank(rank("MASTER", null, 500, { peakTier: "MASTER", peakLp: 300 }))).toMatchObject({
      tier: "MASTER",
      lp: 500,
      isPeak: false,
    });
  });

  it("shows a Master peak over a Diamond current rank", () => {
    expect(displayRank(rank("DIAMOND", 1, 99, { peakTier: "MASTER", peakLp: 0 }))).toMatchObject({
      tier: "MASTER",
      isPeak: true,
    });
  });

  it("keeps a Master current rank at 0 LP over a Diamond I peak", () => {
    expect(displayRank(rank("MASTER", null, 0, { peakTier: "DIAMOND", peakDivision: 1, peakLp: 99 }))).toMatchObject({
      tier: "MASTER",
      isPeak: false,
    });
  });
});
