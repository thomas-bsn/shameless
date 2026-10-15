import { gameWinner, isTeamAOnBlue, monthLabel, seriesScore, shiftMonth } from "./history";

const team = (...riotIds: string[]) =>
  riotIds.map((riotId) => ({ prenom: riotId, riotId, rankTier: null, rankDivision: null, cost: null }));

const game = (winner: string | null, blue: string[], red: string[]) => ({
  winner,
  blueTeam: team(...blue),
  redTeam: team(...red),
});

describe("shiftMonth", () => {
  it("moves from month to month across years", () => {
    expect(shiftMonth({ year: 2026, month: 1 }, -1)).toEqual({ year: 2025, month: 12 });
    expect(shiftMonth({ year: 2025, month: 12 }, 1)).toEqual({ year: 2026, month: 1 });
    expect(shiftMonth({ year: 2026, month: 5 }, 1)).toEqual({ year: 2026, month: 6 });
  });
});

describe("monthLabel", () => {
  it("names the month in French", () => {
    expect(monthLabel({ year: 2026, month: 10 })).toBe("Octobre 2026");
  });
});

describe("isTeamAOnBlue", () => {
  it("finds the side of team A from its reference players", () => {
    expect(isTeamAOnBlue(game("blue", ["a1", "a2"], ["b1", "b2"]), ["a1", "a2"])).toBe(true);
    expect(isTeamAOnBlue(game("blue", ["b1", "b2"], ["a1", "a2"]), ["a1", "a2"])).toBe(false);
  });

  it("puts team A on blue side without a reference", () => {
    expect(isTeamAOnBlue(game("red", ["x"], ["y"]), null)).toBe(true);
  });
});

describe("gameWinner", () => {
  it("names the winning team, whatever its side", () => {
    expect(gameWinner(game("red", ["b1"], ["a1"]), ["a1"])).toBe("A");
    expect(gameWinner(game("red", ["a1"], ["b1"]), ["a1"])).toBe("B");
  });

  it("is null for a game without result", () => {
    expect(gameWinner(game(null, ["a1"], ["b1"]), ["a1"])).toBeNull();
  });
});

describe("seriesScore", () => {
  it("counts the wins of each team across side changes, ignoring games without result", () => {
    const games = [game("blue", ["a1"], ["b1"]), game("blue", ["b1"], ["a1"]), game("red", ["b1"], ["a1"]), game(null, ["a1"], ["b1"])];

    expect(seriesScore(games, ["a1"])).toEqual({ a: 2, b: 1 });
  });
});
