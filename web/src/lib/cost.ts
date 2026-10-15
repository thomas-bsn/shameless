/** Tiers de cost du plus haut au plus bas (CONTEXT.md : cost de 1 à 6, tiers D à SS). */
export const COST_TIERS = ["SS", "S", "A", "B", "C", "D"] as const;

export type CostTier = (typeof COST_TIERS)[number];

/** Le cost qui correspond à chaque tier. */
export const COST_POINTS: Record<CostTier, number> = { SS: 6, S: 5, A: 4, B: 3, C: 2, D: 1 };

/** Un cost fractionnaire (fixé par un admin) est arrondi ; sans cost, le Joueur est en D. */
export function costTier(cost: number): CostTier {
  const rounded = Math.min(6, Math.max(1, Math.round(cost)));
  return COST_TIERS[6 - rounded]!;
}

/** Les tiers non vides, du SS au D ; dans un tier, le plus gros cost d'abord. */
export function groupByCostTier<P extends { prenom: string; currentCost: number }>(players: P[]) {
  const sorted = [...players].sort(
    (a, b) => b.currentCost - a.currentCost || a.prenom.localeCompare(b.prenom, "fr"),
  );
  return COST_TIERS.map((tier) => ({ tier, players: sorted.filter((p) => costTier(p.currentCost) === tier) })).filter(
    (group) => group.players.length > 0,
  );
}
