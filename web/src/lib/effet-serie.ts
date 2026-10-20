export type EquipeGagnante = "A" | "B";

interface SerieDeLaGame {
  bestOf: number;
  teamAWins: number;
  teamBWins: number;
}

/** Le vainqueur d'une Série : la première Équipe à la majorité des games du format, sinon personne encore. */
export function vainqueurDeLaSerie({ bestOf, teamAWins, teamBWins }: SerieDeLaGame): EquipeGagnante | null {
  const requis = Math.floor(bestOf / 2) + 1;
  if (teamAWins >= requis) return "A";
  if (teamBWins >= requis) return "B";
  return null;
}

/** Le score de la Série une fois la game passée de `de` à `vers` (null : sans vainqueur, ou retirée). */
export function serieApres(serie: SerieDeLaGame, de: string | null, vers: string | null): SerieDeLaGame {
  return {
    ...serie,
    teamAWins: serie.teamAWins - (de === "A" ? 1 : 0) + (vers === "A" ? 1 : 0),
    teamBWins: serie.teamBWins - (de === "B" ? 1 : 0) + (vers === "B" ? 1 : 0),
  };
}

export const equipeLabel = (equipe: EquipeGagnante | null): string => (equipe ? `Équipe ${equipe}` : "en cours");
