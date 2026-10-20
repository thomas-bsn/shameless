import type { Game, LigneDeScore, Participant } from "./types.ts";

export type Tolerances = {
  /** Écart de date toléré entre la ligne et le début de la game, en secondes. */
  date: number;
  /** Écart de durée toléré, en secondes. */
  duree: number;
};

export const TOLERANCES_PAR_DEFAUT: Tolerances = { date: 600, duree: 30 };

export type Candidat = { game: Game; participant: Participant };
export type Appariee = { ligne: LigneDeScore } & Candidat;
export type Ambigue = { ligne: LigneDeScore; raison: string; candidats: Candidat[] };
export type SansCorrespondance = { ligne: LigneDeScore; raison: string };

export type Appariement = {
  appariees: Appariee[];
  ambigues: Ambigue[];
  sansCorrespondance: SansCorrespondance[];
};

/** « Kai'Sa », « kaisa » et « KAI SA » sont le même champion. */
const normaliser = (champion: string) => champion.toLowerCase().replace(/[^a-z0-9]/g, "");

/**
 * Apparie chaque ligne du fichier de scores à une game (date et durée à la tolérance près) puis à
 * un joueur de cette game (champion et K/D/A identiques). Une ligne n'est appariée que si la
 * correspondance est unique et qu'aucune autre ligne ne vise le même joueur de la même game ;
 * sinon elle est rapportée comme ambiguë ou sans correspondance, jamais devinée.
 */
export function apparier(lignes: LigneDeScore[], games: Game[], tolerances: Tolerances = TOLERANCES_PAR_DEFAUT): Appariement {
  const resultat: Appariement = { appariees: [], ambigues: [], sansCorrespondance: [] };
  const uniques: Appariee[] = [];

  for (const ligne of lignes) {
    const proches = games.filter(
      (g) => Math.abs(g.debut - ligne.date) / 1000 <= tolerances.date && Math.abs(g.duree - ligne.duree) <= tolerances.duree,
    );
    if (proches.length === 0) {
      resultat.sansCorrespondance.push({ ligne, raison: "aucune game à cette date et avec cette durée (à la tolérance près)" });
      continue;
    }
    const candidats = proches.flatMap((game) =>
      game.participants
        .filter(
          (p) =>
            normaliser(p.championName) === normaliser(ligne.champion) &&
            p.kills === ligne.kills &&
            p.deaths === ligne.deaths &&
            p.assists === ligne.assists,
        )
        .map((participant) => ({ game, participant })),
    );
    if (candidats.length === 0) {
      resultat.sansCorrespondance.push({ ligne, raison: `game trouvée (${proches.map((g) => g.matchId).join(", ")}) mais aucun joueur ne correspond au champion et au K/D/A` });
    } else if (candidats.length > 1) {
      resultat.ambigues.push({ ligne, raison: "plusieurs joueurs ou games possibles", candidats });
    } else {
      uniques.push({ ligne, ...candidats[0]! });
    }
  }

  for (const a of uniques) {
    const memeCible = uniques.filter((b) => b.game === a.game && b.participant === a.participant);
    if (memeCible.length > 1) {
      resultat.ambigues.push({
        ligne: a.ligne,
        raison: `même joueur que les lignes ${memeCible.filter((b) => b !== a).map((b) => b.ligne.numero).join(", ")}`,
        candidats: [{ game: a.game, participant: a.participant }],
      });
    } else {
      resultat.appariees.push(a);
    }
  }
  resultat.appariees.sort((x, y) => x.ligne.numero - y.ligne.numero);
  resultat.ambigues.sort((x, y) => x.ligne.numero - y.ligne.numero);
  return resultat;
}
