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
