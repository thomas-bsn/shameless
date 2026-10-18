/** Une date seule, à l'heure locale : « 12/10/2026 ». */
export const dateLabel = (iso: string) =>
  new Date(iso).toLocaleDateString("fr-FR", { day: "2-digit", month: "2-digit", year: "numeric" });

/** Le jour, la date et l'heure d'une Game ou d'un clip, à l'heure locale : « ven. 16 oct., 22:40 ». */
export const dateHeureLabel = (iso: string) =>
  new Date(iso).toLocaleString("fr-FR", { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
