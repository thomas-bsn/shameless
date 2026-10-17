import { dureeLabel, kdaLabel } from "./game";

describe("dureeLabel", () => {
  it("écrit une durée en minutes:secondes", () => {
    expect(dureeLabel(1834)).toBe("30:34");
    expect(dureeLabel(65)).toBe("1:05");
  });
});

describe("kdaLabel", () => {
  it("écrit kills/morts/assists, avec un tiret pour une stat inconnue", () => {
    expect(kdaLabel({ kills: 7, deaths: 2, assists: 9 })).toBe("7/2/9");
    expect(kdaLabel({ kills: 1, deaths: null, assists: null })).toBe("1/–/–");
  });
});
