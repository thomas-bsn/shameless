import { rankLabel } from "./ranks";

describe("rankLabel", () => {
  it("names the tier, the division in roman numerals and the LP", () => {
    expect(rankLabel("GOLD", 2, 40)).toBe("Gold II · 40 LP");
    expect(rankLabel("emerald", 4, null)).toBe("Emerald IV · 0 LP");
  });

  it("has no division from Master up", () => {
    expect(rankLabel("GRANDMASTER", 1, 512)).toBe("Grandmaster · 512 LP");
  });

  it("says Unranked without a rank", () => {
    expect(rankLabel(null, null, null)).toBe("Unranked");
    expect(rankLabel("UNRANKED", null, 0)).toBe("Unranked");
  });
});
