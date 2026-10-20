import type { Game, Participant } from "./types.ts";

/**
 * La note de performance de l'ADR 0011 (`api/Shameless.Application/Stats/NoteDePerformance.cs`,
 * `ReferencesDeNote.cs`), paramétrable, avec quatre critères de plus que l'API pour le calage :
 * l'or par minute et la part de l'équipe (kills, dégâts, or), plus une composante « victoire ».
 * Les critères de l'ADR gardent leurs poids ; les nouveaux démarrent à 0, donc les paramètres par
 * défaut redonnent exactement la note de l'API.
 */

export const ROLES = ["TOP", "JUNGLE", "MID", "ADC", "SUPPORT", "INCONNU"] as const;
export type Role = (typeof ROLES)[number];

export const CRITERES = [
  "kda",
  "participationAuxKills",
  "csParMinute",
  "degatsParMinute",
  "visionParMinute",
  "objectifsParMinute",
  "orParMinute",
  "partDesKills",
  "partDesDegats",
  "partDeLOr",
] as const;
export type Critere = (typeof CRITERES)[number];

/** Poids (relatif : seule la proportion compte) et références d'un critère : 0 jusqu'à `faible`, 50 à `moyen`, 100 dès `excellent`. */
export type ReferenceDeCritere = { poids: number; faible: number; moyen: number; excellent: number };

export type ParametresDeRole = {
  criteres: Record<Critere, ReferenceDeCritere>;
  /** Poids de la composante victoire (score 100 si la game est gagnée, 0 sinon), du même ordre que les poids des critères. */
  poidsVictoire: number;
};
export type Parametres = { roles: Record<Role, ParametresDeRole> };

export type Mesures = Partial<Record<Critere, number>>;

export type NoteCalculee = {
  /** Note de 0 à 100, non arrondie. */
  note: number;
  role: Role;
  composantes: { critere: Critere | "victoire"; valeur: number; score: number; poids: number }[];
};

const ref = (poids: number, faible: number, moyen: number, excellent: number): ReferenceDeCritere => ({ poids, faible, moyen, excellent });

/** Références et poids de l'API (ADR 0011) pour les six critères d'origine : [poids, faible, moyen, excellent]. */
type Six = [kda: number[], kp: number[], cs: number[], degats: number[], vision: number[], objectifs: number[]];
const API: Record<Exclude<Role, "INCONNU">, Six> = {
  TOP: [[20, 1, 2.5, 5], [15, 0.2, 0.4, 0.6], [20, 4.5, 6.5, 8.5], [20, 300, 600, 900], [10, 0.3, 0.6, 1], [15, 200, 450, 800]],
  JUNGLE: [[20, 1, 3, 6], [25, 0.35, 0.6, 0.8], [10, 3.5, 5.5, 7], [10, 250, 450, 700], [15, 0.5, 1, 1.5], [20, 300, 600, 1000]],
  MID: [[20, 1, 3, 6], [20, 0.3, 0.5, 0.7], [20, 5, 7, 9], [25, 400, 700, 1000], [5, 0.4, 0.8, 1.2], [10, 100, 300, 600]],
  ADC: [[20, 1, 3, 6], [15, 0.3, 0.5, 0.7], [25, 5.5, 7.5, 9.5], [25, 400, 750, 1100], [5, 0.3, 0.6, 1], [10, 200, 500, 900]],
  // Le support n'est pas noté sur le CS ni les objectifs (poids 0) ; références du mid gardées pour pouvoir les ajuster.
  SUPPORT: [[25, 1.5, 3.5, 7], [30, 0.4, 0.6, 0.8], [0, 5, 7, 9], [10, 150, 300, 500], [35, 1, 2, 3], [0, 100, 300, 600]],
};

/** Références de départ des nouveaux critères (ordres de grandeur SoloQ), poids 0. */
const NOUVEAUX: Record<Exclude<Role, "INCONNU">, [or: number[], kills: number[], degats: number[], orPart: number[]]> = {
  TOP: [[250, 360, 450], [0.08, 0.2, 0.32], [0.12, 0.22, 0.32], [0.14, 0.21, 0.28]],
  JUNGLE: [[230, 330, 420], [0.08, 0.2, 0.32], [0.08, 0.16, 0.24], [0.12, 0.18, 0.24]],
  MID: [[280, 390, 480], [0.1, 0.22, 0.34], [0.14, 0.24, 0.34], [0.16, 0.22, 0.29]],
  ADC: [[290, 400, 500], [0.1, 0.22, 0.34], [0.16, 0.26, 0.36], [0.17, 0.23, 0.3]],
  SUPPORT: [[170, 240, 310], [0.02, 0.08, 0.14], [0.03, 0.08, 0.14], [0.07, 0.1, 0.13]],
};

function pourRole(six: Six, nouveaux: [number[], number[], number[], number[]], poidsEgaux = false): ParametresDeRole {
  const [kda, kp, cs, degats, vision, objectifs] = six.map((r) => ref(poidsEgaux ? 1 : r[0]!, r[1]!, r[2]!, r[3]!)) as [ReferenceDeCritere, ReferenceDeCritere, ReferenceDeCritere, ReferenceDeCritere, ReferenceDeCritere, ReferenceDeCritere];
  const [or, kills, partDegats, partOr] = nouveaux.map((r) => ref(0, r[0]!, r[1]!, r[2]!)) as [ReferenceDeCritere, ReferenceDeCritere, ReferenceDeCritere, ReferenceDeCritere];
  return {
    criteres: {
      kda,
      participationAuxKills: kp,
      csParMinute: cs,
      degatsParMinute: degats,
      visionParMinute: vision,
      objectifsParMinute: objectifs,
      orParMinute: or,
      partDesKills: kills,
      partDesDegats: partDegats,
      partDeLOr: partOr,
    },
    poidsVictoire: 0,
  };
}

/** Les paramètres de l'API (ADR 0011), nouveaux critères et victoire à poids 0. Rôle inconnu : références du mid, six critères d'origine à poids égaux. */
export function parametresParDefaut(): Parametres {
  const roles = {} as Record<Role, ParametresDeRole>;
  for (const r of ["TOP", "JUNGLE", "MID", "ADC", "SUPPORT"] as const) roles[r] = pourRole(API[r], NOUVEAUX[r]);
  roles.INCONNU = pourRole(API.MID, NOUVEAUX.MID, true);
  return structuredClone({ roles });
}

const POSITIONS: Record<string, Role> = { TOP: "TOP", JUNGLE: "JUNGLE", MIDDLE: "MID", BOTTOM: "ADC", UTILITY: "SUPPORT" };

/** Le rôle joué selon match-v5 : `teamPosition`, sinon `individualPosition`, sinon inconnu. */
export function roleDe(p: Participant): Role {
  return POSITIONS[(p.teamPosition ?? "").toUpperCase()] ?? POSITIONS[(p.individualPosition ?? "").toUpperCase()] ?? "INCONNU";
}

/** Les valeurs mesurables d'un joueur dans sa game ; un critère manque si une de ses stats, la durée ou le total de l'équipe manque. */
export function mesurer(game: Game, p: Participant): Mesures {
  const m: Mesures = {};
  const equipe = game.participants.filter((x) => x.teamId !== undefined && x.teamId === p.teamId);
  const total = (champ: "kills" | "totalDamageDealtToChampions" | "goldEarned") =>
    equipe.length > 0 && equipe.every((x) => typeof x[champ] === "number") ? equipe.reduce((s, x) => s + (x[champ] as number), 0) : undefined;
  const equipeKills = total("kills");
  const equipeDegats = total("totalDamageDealtToChampions");
  const equipeOr = total("goldEarned");

  const { kills, deaths, assists } = p;
  if (kills !== undefined && deaths !== undefined && assists !== undefined) m.kda = (kills + assists) / Math.max(1, deaths);
  if (kills !== undefined && assists !== undefined && equipeKills && equipeKills > 0) m.participationAuxKills = Math.min(1, (kills + assists) / equipeKills);
  if (kills !== undefined && equipeKills && equipeKills > 0) m.partDesKills = kills / equipeKills;
  if (p.totalDamageDealtToChampions !== undefined && equipeDegats && equipeDegats > 0) m.partDesDegats = p.totalDamageDealtToChampions / equipeDegats;
  if (p.goldEarned !== undefined && equipeOr && equipeOr > 0) m.partDeLOr = p.goldEarned / equipeOr;

  if (game.duree > 0) {
    const minutes = game.duree / 60;
    if (p.totalMinionsKilled !== undefined && p.neutralMinionsKilled !== undefined) m.csParMinute = (p.totalMinionsKilled + p.neutralMinionsKilled) / minutes;
    if (p.totalDamageDealtToChampions !== undefined) m.degatsParMinute = p.totalDamageDealtToChampions / minutes;
    if (p.visionScore !== undefined) m.visionParMinute = p.visionScore / minutes;
    if (p.damageDealtToObjectives !== undefined) m.objectifsParMinute = p.damageDealtToObjectives / minutes;
    if (p.goldEarned !== undefined) m.orParMinute = p.goldEarned / minutes;
  }
  return m;
}

/** 0 jusqu'à faible, 50 à moyen, 100 dès excellent ; linéaire entre deux références. */
export function score(valeur: number, r: ReferenceDeCritere): number {
  if (valeur <= r.faible) return 0;
  if (valeur >= r.excellent) return 100;
  return valeur < r.moyen
    ? (50 * (valeur - r.faible)) / (r.moyen - r.faible)
    : 50 + (50 * (valeur - r.moyen)) / (r.excellent - r.moyen);
}

/** La note à partir de mesures déjà extraites ; null si aucun critère de poids positif n'est mesurable. */
export function noterMesures(mesures: Mesures, gagnee: boolean | undefined, role: Role, params: Parametres): NoteCalculee | null {
  const pr = params.roles[role];
  const composantes: NoteCalculee["composantes"] = [];
  for (const c of CRITERES) {
    const valeur = mesures[c];
    const r = pr.criteres[c];
    if (valeur !== undefined && r.poids > 0) composantes.push({ critere: c, valeur, score: score(valeur, r), poids: r.poids });
  }
  if (gagnee !== undefined && pr.poidsVictoire > 0) composantes.push({ critere: "victoire", valeur: gagnee ? 1 : 0, score: gagnee ? 100 : 0, poids: pr.poidsVictoire });
  const poidsTotal = composantes.reduce((s, c) => s + c.poids, 0);
  if (poidsTotal <= 0) return null;
  const note = composantes.reduce((s, c) => s + c.score * c.poids, 0) / poidsTotal;
  return { note, role, composantes: composantes.map((c) => ({ ...c, poids: c.poids / poidsTotal })) };
}

/** La note d'un joueur dans une game avec ces paramètres, rôle lu dans match-v5 ; `note` vaut NaN si rien n'est mesurable. */
export function noter(game: Game, p: Participant, params: Parametres): NoteCalculee {
  const role = roleDe(p);
  return noterMesures(mesurer(game, p), p.win, role, params) ?? { note: NaN, role, composantes: [] };
}
