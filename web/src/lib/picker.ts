import type { Schemas } from "@/api/client";

type Picker = Schemas["PickerResponse"];

/**
 * L'étape de la session de Picker, déduite de l'état de l'API : sélection des dix, choix de la
 * méthode, Draft en cours, puis Équipes formées (Autorole et validation).
 */
export type PickerStep = "selection" | "methode" | "draft" | "equipes";

export function pickerStep(picker: Picker | null): PickerStep {
  if (!picker) return "selection";
  if (picker.canValidate) return "equipes";
  if (picker.methode === "Draft" && picker.equipeAuTrait) return "draft";
  return "methode";
}

const ROLE_ORDER: Schemas["Role"][] = ["Top", "Jungle", "Mid", "Adc", "Support"];

/** Une Équipe du top au support quand l'Autorole a attribué les rôles ; sinon dans son ordre. */
export function parRole<P extends { role?: Schemas["Role"] | null }>(team: P[]): P[] {
  const rank = (p: P) => (p.role ? ROLE_ORDER.indexOf(p.role) : ROLE_ORDER.length);
  return [...team].sort((a, b) => rank(a) - rank(b));
}

/** Les joueurs que la Draft n'a pas encore placés dans une Équipe (ni capitaines ni pris). */
export function joueursLibres(picker: Picker) {
  const places = new Set([...picker.teamA, ...picker.teamB].map((p) => p.playerId));
  return picker.players.filter((p) => !places.has(p.playerId));
}

/**
 * « Ajouter au Picker » : coche, après les joueurs déjà cochés, les Joueurs liés présents en vocal
 * qui ne le sont pas encore, sans dépasser les dix de la sélection.
 */
export function ajouterCandidats(selected: number[], candidats: number[], max = 10): number[] {
  const ajoutes = candidats.filter((id, i) => !selected.includes(id) && candidats.indexOf(id) === i);
  return [...selected, ...ajoutes].slice(0, Math.max(max, selected.length));
}

/** Où en est le Picker, en une phrase : ce que la Soirée affiche pour le reprendre. */
export function pickerProgress(picker: Picker): string {
  if (picker.players.length < 10) return `Sélection, ${picker.players.length} joueurs sur 10`;
  if (picker.canValidate) return "Équipes formées, prêtes à valider";
  if (picker.methode === "Draft" && picker.equipeAuTrait)
    return `Draft, ${picker.teamA.length + picker.teamB.length} joueurs sur 10 choisis`;
  return "Méthode à choisir";
}
