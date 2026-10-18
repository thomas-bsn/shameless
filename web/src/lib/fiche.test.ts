import { decimalLabel, kdaDetailLabel, pointsDeLaCourbe, pourcentLabel, yDuCost } from "./fiche";

describe("fiche", () => {
  it("place le cost 1 en bas et le cost 6 en haut, linéairement", () => {
    expect(yDuCost(6)).toBeLessThan(yDuCost(3.5));
    expect(yDuCost(3.5)).toBeLessThan(yDuCost(1));
    expect(yDuCost(3.5) - yDuCost(6)).toBeCloseTo(yDuCost(1) - yDuCost(3.5));
    expect(yDuCost(9)).toBe(yDuCost(6));
  });

  it("trie les mois et étale les points de gauche à droite", () => {
    const points = pointsDeLaCourbe([
      { mois: "2026-10-01", cost: 6 },
      { mois: "2026-09-01", cost: 5 },
    ]);
    expect(points.map((p) => p.libelle)).toEqual(["septembre", "octobre"]);
    expect(points[0]!.x).toBeLessThan(points[1]!.x);
  });

  it("n'écrit jamais zéro pour une donnée absente", () => {
    expect(pourcentLabel(null)).toBe("–");
    expect(pourcentLabel(0)).toBe("0 %");
    expect(decimalLabel(null)).toBe("–");
    expect(kdaDetailLabel(null)).toBe("– / – / –");
    expect(kdaDetailLabel({ kills: 3, deaths: null, assists: 0 })).toBe("3 / – / 0");
  });
});
