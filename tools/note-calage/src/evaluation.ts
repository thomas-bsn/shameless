import type { Appariee } from "./appariement.ts";
import type { Echantillon } from "./ajustement.ts";
import { mesurer, noterMesures, roleDe, type Parametres } from "./note.ts";

/** Une ligne appariée prête à être notée : sa game (pour regrouper et séparer), son numéro dans le fichier et le score visé. */
export type LigneAEvaluer = { matchId: string; numero: number; place?: number; echantillon: Echantillon };

export function versLignesAEvaluer(appariees: Appariee[]): LigneAEvaluer[] {
  return appariees.map((a) => ({
    matchId: a.game.matchId,
    numero: a.ligne.numero,
    place: a.ligne.place,
    echantillon: {
      mesures: mesurer(a.game, a.participant),
      gagnee: a.participant.win,
      role: roleDe(a.participant),
      score: a.ligne.score,
    },
  }));
}

/** Générateur pseudo-aléatoire déterministe (mulberry32). */
function alea(graine: number) {
  let a = graine | 0;
  return () => {
    a = (a + 0x6d2b79f5) | 0;
    let t = Math.imul(a ^ (a >>> 15), 1 | a);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

/**
 * Sépare les lignes en apprentissage et test, par game : toutes les lignes d'une game vont du même
 * côté, pour que le test porte sur des games jamais vues. Tirage reproductible selon la graine ;
 * au moins une game de chaque côté quand il y en a deux ou plus.
 */
export function separer(lignes: LigneAEvaluer[], fractionTest: number, graine: number): { apprentissage: LigneAEvaluer[]; test: LigneAEvaluer[] } {
  const games = [...new Set(lignes.map((l) => l.matchId))].sort();
  const hasard = alea(graine);
  for (let i = games.length - 1; i > 0; i--) {
    const j = Math.floor(hasard() * (i + 1));
    [games[i], games[j]] = [games[j]!, games[i]!];
  }
  const nbTest = games.length < 2 ? 0 : Math.min(games.length - 1, Math.max(1, Math.round(fractionTest * games.length)));
  const gamesTest = new Set(games.slice(0, nbTest));
  return {
    apprentissage: lignes.filter((l) => !gamesTest.has(l.matchId)),
    test: lignes.filter((l) => gamesTest.has(l.matchId)),
  };
}

export type Evaluation = {
  /** Lignes notées (celles dont au moins un critère est mesurable). */
  lignes: number;
  /** Écart moyen absolu entre la note et le score dpm.lol, en points. */
  ecartMoyen: number;
  /** Games d'au moins deux lignes dont l'ordre est comparable. */
  games: number;
  gamesDansLeMemeOrdre: number;
  /** Part des games où l'ordre par note est exactement l'ordre dpm.lol ; NaN sans game comparable. */
  frequenceDuMemeOrdre: number;
  /** Part des paires de lignes d'une même game rangées dans le même ordre par la note et par dpm.lol. */
  frequenceDesPairesConcordantes: number;
};

/**
 * Note chaque ligne avec les paramètres et la compare au score dpm.lol : écart moyen en points, et
 * accord sur l'ordre dans la game. L'ordre dpm.lol est la place saisie si toutes les lignes de la
 * game en ont une, sinon le score ; une game dont l'ordre dpm.lol a des égalités sans places est ignorée.
 */
export function evaluer(lignes: LigneAEvaluer[], params: Parametres): Evaluation {
  const notees = lignes.flatMap((l) => {
    const n = noterMesures(l.echantillon.mesures, l.echantillon.gagnee, l.echantillon.role, params);
    return n ? [{ ...l, note: n.note }] : [];
  });
  const ecartMoyen = notees.length === 0 ? NaN : notees.reduce((s, l) => s + Math.abs(l.note - l.echantillon.score), 0) / notees.length;

  const parGame = new Map<string, typeof notees>();
  for (const l of notees) parGame.set(l.matchId, [...(parGame.get(l.matchId) ?? []), l]);
  let games = 0;
  let memeOrdre = 0;
  let paires = 0;
  let pairesConcordantes = 0;
  for (const groupe of parGame.values()) {
    if (groupe.length < 2) continue;
    const places = groupe.every((l) => l.place !== undefined);
    // « rang » dpm.lol : plus petit = meilleur
    const rangDpm = (l: (typeof groupe)[number]) => (places ? l.place! : -l.echantillon.score);
    if (new Set(groupe.map(rangDpm)).size < groupe.length) continue;

    games++;
    const ordreNote = [...groupe].sort((a, b) => b.note - a.note).map((l) => l.numero);
    const ordreDpm = [...groupe].sort((a, b) => rangDpm(a) - rangDpm(b)).map((l) => l.numero);
    if (ordreNote.every((numero, i) => numero === ordreDpm[i])) memeOrdre++;
    for (let i = 0; i < groupe.length; i++) {
      for (let j = i + 1; j < groupe.length; j++) {
        const a = groupe[i]!;
        const b = groupe[j]!;
        paires++;
        if (Math.sign(a.note - b.note) === Math.sign(rangDpm(b) - rangDpm(a))) pairesConcordantes++;
      }
    }
  }
  return {
    lignes: notees.length,
    ecartMoyen,
    games,
    gamesDansLeMemeOrdre: memeOrdre,
    frequenceDuMemeOrdre: games === 0 ? NaN : memeOrdre / games,
    frequenceDesPairesConcordantes: paires === 0 ? NaN : pairesConcordantes / paires,
  };
}
