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
