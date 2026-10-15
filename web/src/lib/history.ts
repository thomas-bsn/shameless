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

/** Victoires de chaque Équipe ; une game sans résultat ne compte pas. */
export function seriesScore(games: HistoryGame[]) {
  const winners = games.map(gameWinner);
  return { a: winners.filter((w) => w === "A").length, b: winners.filter((w) => w === "B").length };
}
