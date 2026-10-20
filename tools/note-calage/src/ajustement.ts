import { CRITERES, noterMesures, ROLES, type Mesures, type Parametres, type Role } from "./note.ts";

/** Ce qu'il faut pour noter une ligne appariée sans retoucher aux documents match-v5 : mesures, victoire, rôle et score dpm.lol visé. */
export type Echantillon = { mesures: Mesures; gagnee: boolean | undefined; role: Role; score: number };

export type OptionsAjustement = {
  /** Quels paramètres bouger : les poids (et la victoire), les références Faible/Moyen/Excellent, ou les deux. */
  ajuster: "poids" | "references" | "tout";
  iterations: number;
  /** Pas initial d'Adam, en fraction de l'échelle de chaque paramètre (décroît jusqu'à 1 % de cette valeur). */
  tauxApprentissage: number;
};

export const OPTIONS_PAR_DEFAUT: OptionsAjustement = { ajuster: "tout", iterations: 1500, tauxApprentissage: 0.05 };

/** Écart quadratique moyen entre la note et le score, sur les échantillons notables. */
function erreurQuadratique(echantillons: Echantillon[], params: Parametres): { somme: number; n: number } {
  let somme = 0;
  let n = 0;
  for (const e of echantillons) {
    const note = noterMesures(e.mesures, e.gagnee, e.role, params)?.note;
    if (note === undefined) continue;
    somme += (note - e.score) ** 2;
    n++;
  }
  return { somme, n };
}

/** Écart moyen absolu, en points de score, entre la note et le score dpm.lol. */
export function erreurMoyenne(echantillons: Echantillon[], params: Parametres): number {
  let somme = 0;
  let n = 0;
  for (const e of echantillons) {
    const note = noterMesures(e.mesures, e.gagnee, e.role, params)?.note;
    if (note === undefined) continue;
    somme += Math.abs(note - e.score);
    n++;
  }
  return n === 0 ? NaN : somme / n;
}

type Variable = { lire: () => number; ecrire: (v: number) => void; echelle: number };

/**
 * Cherche les paramètres qui minimisent l'écart quadratique entre la note et le score dpm.lol :
 * descente de gradient (Adam, gradient par différences finies) rôle par rôle, car les paramètres
 * d'un rôle ne jouent que sur les games de ce rôle. Les rôles absents des échantillons et les
 * paramètres hors du groupe demandé ne bougent pas. Les poids ≥ 0 ; Faible < Moyen < Excellent.
 * Les poids retournés sont ramenés à une somme de 100 par rôle (la note n'en dépend que par proportion).
 * Retourne de nouveaux paramètres (le départ n'est pas modifié) et l'écart quadratique moyen final.
 */
export function ajuster(echantillons: Echantillon[], depart: Parametres, options: Partial<OptionsAjustement> = {}): { parametres: Parametres; perte: number } {
  const opt = { ...OPTIONS_PAR_DEFAUT, ...options };
  const parametres = structuredClone(depart);

  for (const role of ROLES) {
    const duRole = echantillons.filter((e) => e.role === role);
    if (duRole.length === 0) continue;
    const pr = parametres.roles[role];

    const variables: Variable[] = [];
    if (opt.ajuster !== "references") {
      for (const c of CRITERES) {
        const r = pr.criteres[c];
        variables.push({ lire: () => r.poids, ecrire: (v) => (r.poids = Math.max(0, v)), echelle: 10 });
      }
      variables.push({ lire: () => pr.poidsVictoire, ecrire: (v) => (pr.poidsVictoire = Math.max(0, v)), echelle: 10 });
    }
    if (opt.ajuster !== "poids") {
      for (const c of CRITERES) {
        const r = pr.criteres[c];
        const largeur = r.excellent - r.faible;
        const ecart = largeur / 100;
        const remettreEnOrdre = () => {
          r.faible = Math.max(0, r.faible);
          r.moyen = Math.max(r.moyen, r.faible + ecart);
          r.excellent = Math.max(r.excellent, r.moyen + ecart);
        };
        variables.push(
          { lire: () => r.faible, ecrire: (v) => { r.faible = Math.min(v, r.moyen - ecart); remettreEnOrdre(); }, echelle: largeur / 4 },
          { lire: () => r.moyen, ecrire: (v) => { r.moyen = v; remettreEnOrdre(); }, echelle: largeur / 4 },
          { lire: () => r.excellent, ecrire: (v) => { r.excellent = v; remettreEnOrdre(); }, echelle: largeur / 4 },
        );
      }
    }

    const perte = () => {
      const { somme, n } = erreurQuadratique(duRole, parametres);
      return n === 0 ? 0 : somme / n;
    };
    descente(variables, perte, opt);

    // La note ne dépend que de la proportion des poids : on les rend lisibles (somme 100).
    const total = CRITERES.reduce((s, c) => s + pr.criteres[c].poids, 0) + pr.poidsVictoire;
    if (opt.ajuster !== "references" && total > 0) {
      for (const c of CRITERES) pr.criteres[c].poids = (100 * pr.criteres[c].poids) / total;
      pr.poidsVictoire = (100 * pr.poidsVictoire) / total;
    }
  }

  const { somme, n } = erreurQuadratique(echantillons, parametres);
  return { parametres, perte: n === 0 ? NaN : somme / n };
}

/** Adam avec gradient par différences centrées ; garde la meilleure position rencontrée. */
function descente(variables: Variable[], perte: () => number, opt: OptionsAjustement) {
  const m = variables.map(() => 0);
  const v = variables.map(() => 0);
  const [b1, b2, eps] = [0.9, 0.999, 1e-8];
  let meilleure = perte();
  let meilleuresValeurs = variables.map((x) => x.lire());

  for (let t = 1; t <= opt.iterations; t++) {
    const taux = opt.tauxApprentissage * (1 - 0.99 * (t / opt.iterations));
    const gradient = variables.map((x) => {
      const centre = x.lire();
      const h = 1e-4 * x.echelle;
      x.ecrire(centre + h);
      const haut = perte();
      x.ecrire(centre - h);
      const bas = perte();
      x.ecrire(centre);
      return (haut - bas) / (2 * h);
    });
    const valeurs = variables.map((x) => x.lire());
    variables.forEach((x, i) => {
      m[i] = b1 * m[i]! + (1 - b1) * gradient[i]!;
      v[i] = b2 * v[i]! + (1 - b2) * gradient[i]! ** 2;
      const mc = m[i]! / (1 - b1 ** t);
      const vc = v[i]! / (1 - b2 ** t);
      x.ecrire(valeurs[i]! - (taux * x.echelle * mc) / (Math.sqrt(vc) + eps));
    });
    const p = perte();
    if (p < meilleure) {
      meilleure = p;
      meilleuresValeurs = variables.map((x) => x.lire());
    }
  }
  variables.forEach((x, i) => x.ecrire(meilleuresValeurs[i]!));
}
