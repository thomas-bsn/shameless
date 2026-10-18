export interface Month {
  year: number;
  month: number;
}

interface HistoryGame {
  winner: string | null;
}

export type Team = "A" | "B";

const MONTHS = [
  "Janvier",
  "Février",
  "Mars",
  "Avril",
  "Mai",
  "Juin",
  "Juillet",
  "Août",
  "Septembre",
  "Octobre",
  "Novembre",
  "Décembre",
];

export function shiftMonth({ year, month }: Month, delta: number): Month {
  const index = year * 12 + (month - 1) + delta;
  return { year: Math.floor(index / 12), month: (index % 12) + 1 };
}

export const monthLabel = ({ year, month }: Month) => `${MONTHS[month - 1]} ${year}`;

/** Équipe gagnante d'une game ; null tant que le résultat n'est pas saisi. */
export function gameWinner(game: HistoryGame): Team | null {
  return game.winner === "A" || game.winner === "B" ? game.winner : null;
}

/** Cases du calendrier d'un mois, semaine commençant le lundi : null = case vide. */
export function calendarCells({ year, month }: Month): (number | null)[] {
  const daysInMonth = new Date(year, month, 0).getDate();
  const offset = (new Date(year, month - 1, 1).getDay() + 6) % 7;
  return [
    ...Array.from<null>({ length: offset }).fill(null),
    ...Array.from({ length: daysInMonth }, (_, i) => i + 1),
  ];
}

interface DatedGame {
  playedAt: string;
}

interface DatedSeries {
  games: DatedGame[];
}

export interface PlayedDay<S extends DatedSeries> {
  count: number;
  series: { series: S; games: S["games"] }[];
}

/** Jours joués d'un mois (jour local -> games), les games étant rangées sous leur Série. */
export function daysPlayed<S extends DatedSeries>(series: S[]): Map<number, PlayedDay<S>> {
  const days = new Map<number, PlayedDay<S>>();
  for (const s of series) {
    for (const game of s.games) {
      const day = new Date(game.playedAt).getDate();
      const entry = days.get(day) ?? { count: 0, series: [] };
      entry.count += 1;
      const group = entry.series.find((x) => x.series === s);
      if (group) group.games.push(game);
      else entry.series.push({ series: s, games: [game] });
      days.set(day, entry);
    }
  }
  return days;
}

/** Victoires de chaque Équipe ; une game sans résultat ne compte pas. */
export function seriesScore(games: HistoryGame[]) {
  const winners = games.map(gameWinner);
  return { a: winners.filter((w) => w === "A").length, b: winners.filter((w) => w === "B").length };
}
