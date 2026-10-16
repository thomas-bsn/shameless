import type { CostTier } from "./cost";
import type { SoloQTier } from "./ranks";

/** Couleurs des tiers SoloQ, reprises de l'ancien front (classes Tailwind, pas de style inline). */
export const soloQTierClasses: Record<SoloQTier, string> = {
  CHALLENGER: "text-yellow-300 border-yellow-300/30 bg-yellow-300/10",
  GRANDMASTER: "text-red-400 border-red-400/30 bg-red-400/10",
  MASTER: "text-purple-400 border-purple-400/30 bg-purple-400/10",
  DIAMOND: "text-sky-400 border-sky-400/30 bg-sky-400/10",
  EMERALD: "text-emerald-400 border-emerald-400/30 bg-emerald-400/10",
  PLATINUM: "text-teal-300 border-teal-300/30 bg-teal-300/10",
  GOLD: "text-amber-400 border-amber-400/30 bg-amber-400/10",
  SILVER: "text-slate-300 border-slate-300/30 bg-slate-300/10",
  BRONZE: "text-orange-700 border-orange-700/30 bg-orange-700/10",
  IRON: "text-zinc-500 border-zinc-500/30 bg-zinc-500/10",
  UNRANKED: "text-muted-foreground border-border bg-muted/30",
};

export const costTierClasses: Record<CostTier, string> = {
  SS: "text-red-400 border-red-400/30 bg-red-400/10",
  S: "text-yellow-300 border-yellow-300/30 bg-yellow-300/10",
  A: "text-emerald-400 border-emerald-400/30 bg-emerald-400/10",
  B: "text-sky-400 border-sky-400/30 bg-sky-400/10",
  C: "text-slate-300 border-slate-300/30 bg-slate-300/10",
  D: "text-zinc-500 border-zinc-500/30 bg-zinc-500/10",
};

/** Couleur du texte d'un Side : blue ou red (historique, Série, Draft de champions). */
export function sideTextClass(side: string): string {
  return side === "red" ? "text-red-400" : "text-sky-400";
}

/** Couleur de la bordure d'un Side, assortie à son texte. */
export function sideBorderClass(side: string): string {
  return side === "red" ? "border-red-400" : "border-sky-400";
}
