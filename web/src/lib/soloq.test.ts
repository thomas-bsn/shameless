import { averageCost, displayRank, groupBySoloQTier, rankScore, scoreToRank, soloQLevel } from "./soloq";

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
