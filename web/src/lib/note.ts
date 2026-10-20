export type PalierDeNote = "or" | "vert" | "bleu" | "orange" | "rouge";

/** Le palier de couleur d'une note sur 100 : 90 or, 75 vert, 50 bleu, 30 orange, en dessous rouge. */
export function palierDeNote(note: number): PalierDeNote {
  if (note >= 90) return "or";
  if (note >= 75) return "vert";
  if (note >= 50) return "bleu";
  if (note >= 30) return "orange";
  return "rouge";
}
