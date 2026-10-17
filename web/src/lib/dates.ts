/** Le jour, la date et l'heure d'une Game ou d'un clip, à l'heure locale : « ven. 16 oct., 22:40 ». */
export const dateHeureLabel = (iso: string) =>
  new Date(iso).toLocaleString("fr-FR", { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });
