import { calendarCells, daysPlayed, gameWinner, monthLabel, seriesScore, shiftMonth } from "./history";

describe("calendarCells", () => {
  it("starts on Monday with empty cells before the 1st and lists every day", () => {
    // 1er octobre 2026 = jeudi : 3 cases vides (lun, mar, mer), puis 31 jours.
    const cells = calendarCells({ year: 2026, month: 10 });
    expect(cells.slice(0, 3)).toEqual([null, null, null]);
    expect(cells[3]).toBe(1);
    expect(cells.filter((c) => c !== null)).toHaveLength(31);
    expect(cells.at(-1)).toBe(31);
  });

  it("has no empty cell when the month starts on a Monday", () => {
    // 1er juin 2026 = lundi.
    expect(calendarCells({ year: 2026, month: 6 })[0]).toBe(1);
  });
});

describe("daysPlayed", () => {
  const g = (id: number, playedAt: string) => ({ id, playedAt, winner: null });
  const series = [
    { seriesId: 5, playedAt: "2026-10-03T12:00:00", games: [g(1, "2026-10-03T12:00:00"), g(2, "2026-10-03T13:00:00")] },
    { seriesId: 0, playedAt: "2026-10-07T12:00:00", games: [g(3, "2026-10-07T12:00:00")] },
    { seriesId: 6, playedAt: "2026-10-07T14:00:00", games: [g(4, "2026-10-07T14:00:00")] },
  ];

  it("counts the games of each played day, hors série included", () => {
    const days = daysPlayed(series);
    expect(days.get(3)?.count).toBe(2);
    expect(days.get(7)?.count).toBe(2);
    expect(days.get(4)).toBeUndefined();
  });

  it("groups each day's games under their series", () => {
    const day7 = daysPlayed(series).get(7)!;
    expect(day7.series.map((s) => [s.series.seriesId, s.games.map((x) => x.id)])).toEqual([
      [0, [3]],
      [6, [4]],
    ]);
  });
});

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
