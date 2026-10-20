/** Une date seule, à l'heure locale : « 12/10/2026 ». */
export const dateLabel = (iso: string) =>
  new Date(iso).toLocaleDateString("fr-FR", { day: "2-digit", month: "2-digit", year: "numeric" });

/** Le jour, la date et l'heure d'une Game ou d'un clip, à l'heure locale : « ven. 16 oct., 22:40 ». */
export const dateHeureLabel = (iso: string) =>
  new Date(iso).toLocaleString("fr-FR", { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });

/** Le temps écoulé, en une unité : « il y a 12 min », « il y a 3 h », « il y a 15 j ». */
export const ilYALabel = (iso: string, maintenant: number = Date.now()) => {
  const minutes = Math.max(0, Math.floor((maintenant - new Date(iso).getTime()) / 60_000));
  if (minutes < 60) return `il y a ${minutes} min`;
  if (minutes < 24 * 60) return `il y a ${Math.floor(minutes / 60)} h`;
  return `il y a ${Math.floor(minutes / (24 * 60))} j`;
};
