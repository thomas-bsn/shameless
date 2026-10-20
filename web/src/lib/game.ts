/** Une durée de game en minutes:secondes (ex. 30:34). */
export function dureeLabel(seconds: number): string {
  const s = Math.max(0, Math.round(seconds));
  return `${Math.floor(s / 60)}:${String(s % 60).padStart(2, "0")}`;
}

interface Kda {
  kills: number | null;
  deaths: number | null;
  assists: number | null;
}

/** Kills/morts/assists ; une stat que le Companion n'a pas envoyée s'écrit « – ». */
export function kdaLabel({ kills, deaths, assists }: Kda): string {
  return [kills, deaths, assists].map((v) => (v == null ? "–" : String(v))).join("/");
}

/** Une stat chiffrée ; une stat absente s'écrit « – », jamais 0. */
export const statLabel = (value: number | null): string => (value == null ? "–" : String(value));

/** Un total en milliers à la française (« 14,2 k ») ; « – » s'il est absent. */
export function millierLabel(value: number | null): string {
  if (value == null) return "–";
  return `${(value / 1000).toLocaleString("fr-FR", { minimumFractionDigits: 1, maximumFractionDigits: 1 })} k`;
}

/** Les CS : sbires + monstres connus ; null quand aucun des deux n'est connu. */
export function csTotal({
  minionsKilled,
  neutralMinionsKilled,
}: {
  minionsKilled: number | null;
  neutralMinionsKilled: number | null;
}): number | null {
  if (minionsKilled == null && neutralMinionsKilled == null) return null;
  return (minionsKilled ?? 0) + (neutralMinionsKilled ?? 0);
}

/** Le poste joué tel que l'API le donne (Top, Jungle, Mid, Adc, Support) ; souvent absent en custom. */
export const posteLabel = (role: string | null | undefined): string | null =>
  role ? (role === "Adc" ? "ADC" : role) : null;

const ORDRE_DES_ROLES = ["Top", "Jungle", "Mid", "Adc", "Support"];

/**
 * Les joueurs d'une équipe dans l'ordre des voies (Top, Jungle, Mid, ADC, Support) d'après le rôle joué.
 * Ceux dont le rôle est inconnu viennent après, dans leur ordre d'origine ; sans aucun rôle connu,
 * l'ordre d'origine est gardé. La liste reçue n'est pas modifiée.
 */
export function trierParRole<T extends { stats?: { position?: string | null } | null }>(joueurs: readonly T[]): T[] {
  const rang = (j: T) => {
    const i = ORDRE_DES_ROLES.indexOf(j.stats?.position ?? "");
    return i === -1 ? ORDRE_DES_ROLES.length : i;
  };
  return joueurs
    .map((j, ordre) => ({ j, ordre }))
    .sort((x, y) => rang(x.j) - rang(y.j) || x.ordre - y.ordre)
    .map((x) => x.j);
}

/** Le ton d'une note sur 100 : 75 et plus bonne, 50 et plus moyenne, sinon mauvaise. */
export function noteTone(note: number): "good" | "mid" | "bad" {
  return note >= 75 ? "good" : note >= 50 ? "mid" : "bad";
}

interface Participant {
  stats: unknown;
  performance: unknown;
}

/** Une Game terminée dont aucun joueur n'a de stats : son résultat a été saisi à la main. */
export function estSaisieAMain(game: { winner: string | null; teamA: Participant[]; teamB: Participant[] }): boolean {
  if (game.winner == null) return false;
  return [...game.teamA, ...game.teamB].every((p) => p.stats == null && p.performance == null);
}

/** La somme des valeurs connues ; null quand aucune ne l'est (jamais un faux 0). */
export function sommeConnue(values: Array<number | null | undefined>): number | null {
  const connues = values.filter((v): v is number => v != null);
  return connues.length === 0 ? null : connues.reduce((a, b) => a + b, 0);
}

/** Une valeur par minute ; null sans valeur ou sans durée connue (plutôt que fausse). */
export function parMinute(value: number | null, durationSeconds: number | null | undefined): number | null {
  if (value == null || durationSeconds == null || durationSeconds <= 0) return null;
  return value / (durationSeconds / 60);
}

/** Un nombre à la française, avec le nombre de décimales demandé ; « – » s'il est absent. */
export function nombreLabel(value: number | null, decimales = 0): string {
  if (value == null) return "–";
  return value.toLocaleString("fr-FR", { minimumFractionDigits: decimales, maximumFractionDigits: decimales });
}

/** Une valeur par minute écrite (« 667 /min ») ; « – » si elle est absente. */
export const parMinuteLabel = (value: number | null, decimales = 0): string =>
  value == null ? "–" : `${nombreLabel(value, decimales)} /min`;

/** La part d'un total en pourcentage entier (« 67 % ») ; « – » si l'un des deux est absent ou le total nul. */
export function partLabel(value: number | null, total: number | null): string {
  if (value == null || total == null || total <= 0) return "–";
  return `${Math.round((value / total) * 100)} %`;
}

/** Le ratio (kills + assists) / morts, au moins une mort comme l'API ; null sans kills, morts ou assists. */
export function ratioKda({ kills, deaths, assists }: Kda): number | null {
  if (kills == null || deaths == null || assists == null) return null;
  return (kills + assists) / Math.max(1, deaths);
}

/** Les CS par minute ; null sans CS connus ou sans durée. */
export function csParMinute(
  stats: { minionsKilled: number | null; neutralMinionsKilled: number | null },
  dureeSecondes: number | null,
): number | null {
  const cs = csTotal(stats);
  return cs == null || !dureeSecondes ? null : cs / (dureeSecondes / 60);
}
