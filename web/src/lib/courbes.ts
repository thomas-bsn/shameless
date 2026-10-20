import type { Schemas } from "@/api/client";
import { nombreLabel } from "@/lib/game";

export type Chronologie = Schemas["ChronologieDeGameResponse"];
type JoueurDuPoint = Schemas["JoueurDuPointDto"];

/** La nature d'une série, telle que l'API la donne (ADR 0022). */
export type Nature = "reelle" | "estimee" | "indisponible";

export const NATURE_LABEL: Record<Nature, string> = {
  reelle: "réel",
  estimee: "estimé",
  indisponible: "indisponible",
};

export interface Metrique {
  id: "or" | "cs" | "kills" | "niveau" | "vision" | "degats";
  /** Le nom du sélecteur. */
  nom: string;
  /** Le nom dans un titre : « Avantage d'or », « Avantage de kills ». */
  avantage: string;
  /** Le titre d'une courbe de joueur : « Or de Bob ». */
  deJoueur: (prenom: string) => string;
  /** Le nom de la série dans la chronologie de l'API. */
  serie: string;
  valeur: (j: JoueurDuPoint) => number | null;
  /** Un groupe se résume par la moyenne de ses joueurs plutôt que par la somme. */
  moyenne?: boolean;
}

export const METRIQUES: Metrique[] = [
  { id: "or", nom: "Or", avantage: "Avantage d'or", deJoueur: (p) => `Or de ${p}`, serie: "or", valeur: (j) => j.orEstime },
  { id: "cs", nom: "CS", avantage: "Avantage de CS", deJoueur: (p) => `CS de ${p}`, serie: "cs", valeur: (j) => j.cs },
  { id: "kills", nom: "Kills", avantage: "Avantage de kills", deJoueur: (p) => `Kills de ${p}`, serie: "kills", valeur: (j) => j.kills },
  { id: "niveau", nom: "Niveau", avantage: "Avantage de niveau", deJoueur: (p) => `Niveau de ${p}`, serie: "niveau", valeur: (j) => j.niveau, moyenne: true },
  { id: "vision", nom: "Vision", avantage: "Avantage de vision", deJoueur: (p) => `Vision de ${p}`, serie: "vision", valeur: (j) => j.vision },
  // L'API ne fournit pas les dégâts en custom : la série reste indisponible, on n'en invente aucun point.
  { id: "degats", nom: "Dégâts", avantage: "Avantage de dégâts", deJoueur: (p) => `Dégâts de ${p}`, serie: "degats", valeur: () => null },
];

export const metrique = (id: Metrique["id"]): Metrique => METRIQUES.find((m) => m.id === id)!;

/** La nature d'une série ; une série que l'API ne mentionne pas est indisponible. */
export function natureDe(chronologie: Chronologie, serie: string): Nature {
  const nature = chronologie.series.find((s) => s.nom === serie)?.nature;
  return nature === "reelle" || nature === "estimee" ? nature : "indisponible";
}

export interface PointDeCourbe {
  t: number;
  v: number;
}

/**
 * La valeur d'un groupe de joueurs (somme, ou moyenne) à chaque point où tous ses joueurs ont une valeur.
 * Un point où l'un manque est sauté : aucune valeur n'est inventée.
 */
export function courbeDeGroupe(chronologie: Chronologie, joueurs: number[], m: Metrique): PointDeCourbe[] {
  if (joueurs.length === 0) return [];
  const courbe: PointDeCourbe[] = [];
  for (const point of chronologie.points) {
    const valeurs = joueurs.map((id) => {
      const j = point.joueurs.find((x) => x.playerId === id);
      return j ? m.valeur(j) : null;
    });
    if (valeurs.some((v) => v == null)) continue;
    const somme = (valeurs as number[]).reduce((a, b) => a + b, 0);
    courbe.push({ t: point.t, v: m.moyenne ? somme / joueurs.length : somme });
  }
  return courbe;
}

/** L'écart point à point entre deux courbes, sur les instants qu'elles ont en commun. */
export function ecart(a: PointDeCourbe[], b: PointDeCourbe[]): PointDeCourbe[] {
  const parT = new Map(b.map((p) => [p.t, p.v]));
  return a.flatMap((p) => (parT.has(p.t) ? [{ t: p.t, v: p.v - parT.get(p.t)! }] : []));
}

/** Un nombre signé (« +2 000 », « −350 », « 0 »). */
export function signeLabel(valeur: number, decimales = 0): string {
  if (valeur === 0) return "0";
  return `${valeur > 0 ? "+" : "−"}${nombreLabel(Math.abs(valeur), decimales)}`;
}

/** Des secondes en « m:ss ». */
export function horloge(secondes: number): string {
  const s = Math.round(secondes);
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;
}

/**
 * Un tracé lissé (Catmull-Rom converti en courbes de Bézier) passant par chaque point.
 * Les points de contrôle restent dans l'intervalle vertical des voisins pour éviter les dépassements.
 */
export function traceLisse(points: Array<[number, number]>): string {
  if (points.length === 0) return "";
  const f = (n: number) => Math.round(n * 10) / 10;
  let d = `M${f(points[0][0])} ${f(points[0][1])}`;
  for (let i = 0; i < points.length - 1; i++) {
    const p0 = points[i - 1] ?? points[i];
    const p1 = points[i];
    const p2 = points[i + 1];
    const p3 = points[i + 2] ?? p2;
    const borne = (y: number) => Math.min(Math.max(y, Math.min(p1[1], p2[1])), Math.max(p1[1], p2[1]));
    const c1: [number, number] = [p1[0] + (p2[0] - p0[0]) / 6, borne(p1[1] + (p2[1] - p0[1]) / 6)];
    const c2: [number, number] = [p2[0] - (p3[0] - p1[0]) / 6, borne(p2[1] - (p3[1] - p1[1]) / 6)];
    d += ` C${f(c1[0])} ${f(c1[1])} ${f(c2[0])} ${f(c2[1])} ${f(p2[0])} ${f(p2[1])}`;
  }
  return d;
}
