export const COST_MIN = 1;
export const COST_MAX = 6;

/** Un pourcentage entier ; une valeur absente s'écrit « – », jamais 0. */
export const pourcentLabel = (value: number | null): string => (value == null ? "–" : `${Math.round(value)} %`);

/** Une stat décimale à la française (« 3,8 ») ; absente, « – ». */
export const decimalLabel = (value: number | null, digits = 1): string =>
  value == null ? "–" : value.toLocaleString("fr-FR", { minimumFractionDigits: 0, maximumFractionDigits: digits });

/** Un nombre entier arrondi ; absent, « – ». */
export const entierLabel = (value: number | null): string => (value == null ? "–" : String(Math.round(value)));

/** « 9 / 2 / 7 » ; chaque stat absente s'écrit « – ». */
export const kdaDetailLabel = (
  stats: { kills: number | null; deaths: number | null; assists: number | null } | null,
): string => [stats?.kills, stats?.deaths, stats?.assists].map((v) => (v == null ? "–" : String(v))).join(" / ");

const mois = (iso: string, format: "long" | "short") =>
  new Date(iso).toLocaleDateString("fr-FR", { month: format, timeZone: "UTC" });

export interface PointDeCourbe {
  mois: string;
  libelle: string;
  cost: number;
  x: number;
  y: number;
}

export const COURBE = { largeur: 480, hauteur: 200, gauche: 28, droite: 16, haut: 12, bas: 28 } as const;

/** L'ordonnée d'un cost sur l'échelle de 1 (en bas) à 6 (en haut). */
export function yDuCost(cost: number): number {
  const borne = Math.min(COST_MAX, Math.max(COST_MIN, cost));
  const utile = COURBE.hauteur - COURBE.haut - COURBE.bas;
  return COURBE.haut + ((COST_MAX - borne) / (COST_MAX - COST_MIN)) * utile;
}

/** Les points de la courbe du cost, du plus ancien mois au plus récent, répartis sur la largeur. */
export function pointsDeLaCourbe(historique: Array<{ mois: string; cost: number }>): PointDeCourbe[] {
  const tries = [...historique].sort((a, b) => a.mois.localeCompare(b.mois));
  const utile = COURBE.largeur - COURBE.gauche - COURBE.droite;
  return tries.map((m, i) => ({
    mois: mois(m.mois, "short"),
    libelle: mois(m.mois, "long"),
    cost: m.cost,
    x: COURBE.gauche + (tries.length === 1 ? utile / 2 : (i / (tries.length - 1)) * utile),
    y: yDuCost(m.cost),
  }));
}
