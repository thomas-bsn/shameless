import type { CostTier } from "./cost";
import type { SoloQTier } from "./ranks";

/**
 * Couleurs des tiers SoloQ, reprises de l'ancien front (classes Tailwind, pas de style inline).
 * Le thème clair prend les nuances foncées, le thème sombre celles de l'ancien front : lisibles dans les deux.
 */
export const soloQTierClasses: Record<SoloQTier, string> = {
  CHALLENGER: "text-amber-600 border-amber-600/30 bg-amber-600/10 dark:text-yellow-300 dark:border-yellow-300/30 dark:bg-yellow-300/10",
  GRANDMASTER: "text-red-600 border-red-600/30 bg-red-600/10 dark:text-red-400 dark:border-red-400/30 dark:bg-red-400/10",
  MASTER: "text-purple-600 border-purple-600/30 bg-purple-600/10 dark:text-purple-400 dark:border-purple-400/30 dark:bg-purple-400/10",
  DIAMOND: "text-sky-600 border-sky-600/30 bg-sky-600/10 dark:text-sky-400 dark:border-sky-400/30 dark:bg-sky-400/10",
  EMERALD: "text-emerald-600 border-emerald-600/30 bg-emerald-600/10 dark:text-emerald-400 dark:border-emerald-400/30 dark:bg-emerald-400/10",
  PLATINUM: "text-teal-600 border-teal-600/30 bg-teal-600/10 dark:text-teal-300 dark:border-teal-300/30 dark:bg-teal-300/10",
  GOLD: "text-amber-600 border-amber-600/30 bg-amber-600/10 dark:text-amber-400 dark:border-amber-400/30 dark:bg-amber-400/10",
  SILVER: "text-slate-500 border-slate-500/30 bg-slate-500/10 dark:text-slate-300 dark:border-slate-300/30 dark:bg-slate-300/10",
  BRONZE: "text-orange-700 border-orange-700/30 bg-orange-700/10",
  IRON: "text-zinc-600 border-zinc-600/30 bg-zinc-600/10 dark:text-zinc-500 dark:border-zinc-500/30 dark:bg-zinc-500/10",
  UNRANKED: "text-muted-foreground border-border bg-muted/30",
};

export const costTierClasses: Record<CostTier, string> = {
  SS: "text-red-600 border-red-600/30 bg-red-600/10 dark:text-red-400 dark:border-red-400/30 dark:bg-red-400/10",
  S: "text-amber-600 border-amber-600/30 bg-amber-600/10 dark:text-yellow-300 dark:border-yellow-300/30 dark:bg-yellow-300/10",
  A: "text-emerald-600 border-emerald-600/30 bg-emerald-600/10 dark:text-emerald-400 dark:border-emerald-400/30 dark:bg-emerald-400/10",
  B: "text-sky-600 border-sky-600/30 bg-sky-600/10 dark:text-sky-400 dark:border-sky-400/30 dark:bg-sky-400/10",
  C: "text-slate-500 border-slate-500/30 bg-slate-500/10 dark:text-slate-300 dark:border-slate-300/30 dark:bg-slate-300/10",
  D: "text-zinc-600 border-zinc-600/30 bg-zinc-600/10 dark:text-zinc-500 dark:border-zinc-500/30 dark:bg-zinc-500/10",
};

/** Couleur du texte d'un Side : blue ou red (historique, Série, Draft de champions). */
export function sideTextClass(side: string): string {
  return side === "red" ? "text-red-600 dark:text-red-400" : "text-sky-600 dark:text-sky-400";
}

/** Couleur de la bordure d'un Side, assortie à son texte. */
export function sideBorderClass(side: string): string {
  return side === "red" ? "border-red-600 dark:border-red-400" : "border-sky-600 dark:border-sky-400";
}
