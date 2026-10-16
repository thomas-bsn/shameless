import type { Schemas } from "@/api/client";

type Outcome = Pick<Schemas["SeriesResponse"], "outcome" | "winningTeam">;

/** Libellé d'un Side ("blue" | "red") : Blue side ou Red side. */
export function sideLabel(side: string): string {
  return side === "red" ? "Red side" : "Blue side";
}

/** Libellé de l'issue d'une Série, tel que l'API la renvoie. */
export function outcomeLabel({ outcome, winningTeam }: Outcome): string {
  switch (outcome) {
    case "Gagnee":
      return `Gagnée par l'Équipe ${winningTeam}`;
    case "Nulle":
      return "Nulle";
    case "Interrompue":
      return "Interrompue";
    default:
      return "En cours";
  }
}

/** La game de la Série qui attend son résultat, ou null entre deux games. */
export function pendingGame(series: Pick<Schemas["SeriesResponse"], "games">): Schemas["SeriesGameResponse"] | null {
  return series.games.find((g) => g.status === "pending") ?? null;
}

/** Les joueurs qui peuvent entrer en Remplacement : ceux hors de la composition courante. */
export function replacementCandidates<P extends { id: number }>(
  players: P[],
  series: Pick<Schemas["SeriesResponse"], "teamA" | "teamB">,
): P[] {
  const inSeries = new Set([...series.teamA, ...series.teamB].map((p) => p.playerId));
  return players.filter((p) => !inSeries.has(p.id));
}
