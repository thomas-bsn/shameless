export interface Month {
  year: number;
  month: number;
}

interface HistoryGame {
  winner: string | null;
  blueTeam: { riotId: string }[];
  redTeam: { riotId: string }[];
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

/**
 * L'Équipe A d'une Série est connue par les RiotId de ses joueurs (teamAReference) ;
 * elle peut changer de side d'une game à l'autre. Sans référence, A est en blue.
 */
export function isTeamAOnBlue(game: HistoryGame, teamAReference: string[] | null): boolean {
  if (!teamAReference?.length) return true;
  return game.blueTeam.some((p) => teamAReference.includes(p.riotId));
}

export function gameWinner(game: HistoryGame, teamAReference: string[] | null): Team | null {
  if (game.winner !== "blue" && game.winner !== "red") return null;
  return (game.winner === "blue") === isTeamAOnBlue(game, teamAReference) ? "A" : "B";
}

/** Victoires de chaque Équipe ; une game sans résultat ne compte pas. */
export function seriesScore(games: HistoryGame[], teamAReference: string[] | null) {
  const winners = games.map((g) => gameWinner(g, teamAReference));
  return { a: winners.filter((w) => w === "A").length, b: winners.filter((w) => w === "B").length };
}
