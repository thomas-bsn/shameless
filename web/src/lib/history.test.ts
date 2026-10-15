import { gameWinner, monthLabel, seriesScore, shiftMonth } from "./history";

const game = (winner: string | null) => ({ winner });

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

describe("gameWinner", () => {
  it("names the winning team", () => {
    expect(gameWinner(game("A"))).toBe("A");
    expect(gameWinner(game("B"))).toBe("B");
  });

  it("is null for a game without result", () => {
    expect(gameWinner(game(null))).toBeNull();
  });
});

describe("seriesScore", () => {
  it("counts the wins of each team, ignoring games without result", () => {
    expect(seriesScore([game("A"), game("B"), game("A"), game(null)])).toEqual({ a: 2, b: 1 });
  });
});
