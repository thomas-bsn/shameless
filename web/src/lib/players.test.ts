import { player } from "@/test/fixtures";
import { matchesPlayerOrRole, sortPlayers } from "./players";

describe("sortPlayers", () => {
  const alice = player({ id: 1, prenom: "Alice", currentCost: 3, rankTier: "GOLD", rankDivision: 2, peakTier: null });
  const bob = player({
    id: 2,
    prenom: "Bob",
    currentCost: 6,
    rankTier: "DIAMOND",
    rankDivision: 1,
    peakTier: "DIAMOND",
    peakDivision: 1,
    peakLp: 10,
  });
  const chloe = player({
    id: 3,
    prenom: "Chloé",
    currentCost: 1,
    rankTier: null,
    rankDivision: null,
    peakTier: "GOLD",
    peakDivision: 4,
    peakLp: 0,
  });
  const winrates = new Map([
    [1, 0.5],
    [2, 0.7],
  ]);
  const names = (list: { prenom: string }[]) => list.map((p) => p.prenom);

  it("sorts by name, ascending then descending", () => {
    expect(names(sortPlayers([bob, chloe, alice], "joueur", "asc", winrates))).toEqual(["Alice", "Bob", "Chloé"]);
    expect(names(sortPlayers([bob, chloe, alice], "joueur", "desc", winrates))).toEqual(["Chloé", "Bob", "Alice"]);
  });

  it("sorts by cost and by SoloQ rank", () => {
    expect(names(sortPlayers([alice, bob, chloe], "cost", "desc", winrates))).toEqual(["Bob", "Alice", "Chloé"]);
    expect(names(sortPlayers([alice, bob, chloe], "rang", "desc", winrates))).toEqual(["Bob", "Alice", "Chloé"]);
  });

  it("always puts players without peak or winrate last", () => {
    expect(names(sortPlayers([alice, bob, chloe], "peak", "desc", winrates))).toEqual(["Bob", "Chloé", "Alice"]);
    expect(names(sortPlayers([alice, bob, chloe], "peak", "asc", winrates))).toEqual(["Chloé", "Bob", "Alice"]);
    expect(names(sortPlayers([alice, bob, chloe], "winrate", "desc", winrates))).toEqual(["Bob", "Alice", "Chloé"]);
    expect(names(sortPlayers([alice, bob, chloe], "winrate", "asc", winrates))).toEqual(["Alice", "Bob", "Chloé"]);
  });
});

describe("matchesPlayerOrRole", () => {
  const kuro = player({ prenom: "Kuro", riotId: "Kuro#EUW", mainRole: "BOTTOM" });

  it("matches the name, the Riot id or the role label, ignoring case and accents", () => {
    expect(matchesPlayerOrRole(kuro, "kur")).toBe(true);
    expect(matchesPlayerOrRole(kuro, "adc")).toBe(true);
    expect(matchesPlayerOrRole(kuro, "ADC ")).toBe(true);
    expect(matchesPlayerOrRole(kuro, "mid")).toBe(false);
  });
});
