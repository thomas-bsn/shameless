import { rankLabel, roleLabel } from "./ranks";

describe("rankLabel", () => {
  it("names the tier, the division in roman numerals and the LP", () => {
    expect(rankLabel("GOLD", 2, 40)).toBe("Gold II · 40 LP");
    expect(rankLabel("emerald", 4, 0)).toBe("Emerald IV · 0 LP");
  });

  it("leaves out unknown LP", () => {
    expect(rankLabel("GOLD", 2, null)).toBe("Gold II");
    expect(rankLabel("MASTER", null, null)).toBe("Master");
  });

  it("has no division from Master up", () => {
    expect(rankLabel("GRANDMASTER", 1, 512)).toBe("Grandmaster · 512 LP");
  });

  it("says Unranked without a rank", () => {
    expect(rankLabel(null, null, null)).toBe("Unranked");
    expect(rankLabel("UNRANKED", null, 0)).toBe("Unranked");
  });
});

describe("roleLabel", () => {
  it("names the Riot positions the way players say them", () => {
    expect(["TOP", "JUNGLE", "MIDDLE", "BOTTOM", "UTILITY"].map(roleLabel)).toEqual([
      "Top",
      "Jungle",
      "Mid",
      "ADC",
      "Support",
    ]);
  });

  it("has nothing to say about an unknown role", () => {
    expect(roleLabel(null)).toBeNull();
    expect(roleLabel("")).toBeNull();
  });
});
