/** Tiers de cost du plus haut au plus bas (CONTEXT.md : cost de 1 à 6, tiers D à SS). */
export const COST_TIERS = ["SS", "S", "A", "B", "C", "D"] as const;

export type CostTier = (typeof COST_TIERS)[number];

/** Le cost qui correspond à chaque tier. */
export const COST_POINTS: Record<CostTier, number> = { SS: 6, S: 5, A: 4, B: 3, C: 2, D: 1 };

/** Un cost en points, à deux décimales au plus (« 2.5 pts »). */
export const points = (cost: number) => `${Number(cost.toFixed(2))} pts`;

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

/** Les six marches de l'escalier, du D au SS ; dans une marche, par prénom (le cost ne départage pas). */
export function stairSteps<P extends { prenom: string; currentCost: number }>(players: P[]) {
  return [...COST_TIERS].reverse().map((tier) => ({
    tier,
    players: players.filter((p) => costTier(p.currentCost) === tier).sort((a, b) => a.prenom.localeCompare(b.prenom, "fr")),
  }));
}

/** Les Joueurs du plus gros cost au plus petit, à égalité par prénom. */
export function rankByCost<P extends { prenom: string; currentCost: number }>(players: P[]) {
  return [...players].sort((a, b) => b.currentCost - a.currentCost || a.prenom.localeCompare(b.prenom, "fr"));
}

/** Un cost à une décimale, à la française (« 4,5 »). */
export const costText = (cost: number) => cost.toFixed(1).replace(".", ",");

/** Le rang en toutes lettres : « 1er », « 2e »… */
export const placeText = (rang: number) => (rang === 1 ? "1er" : `${rang}e`);

/**
 * L'évolution du cost depuis le mois précédent, prête à afficher ; null quand l'API ne la connaît
 * pas (aucun mois précédent). Le sens ne dépend que de la valeur arrondie au dixième.
 */
export function evolutionDuCost(evolution: number | null) {
  if (evolution === null) return null;
  const arrondie = Math.round(evolution * 10) / 10;
  const sens = arrondie > 0 ? "hausse" : arrondie < 0 ? "baisse" : "stable";
  const valeur = costText(Math.abs(arrondie));
  const signe = sens === "hausse" ? "▲" : sens === "baisse" ? "▼" : "=";
  const dit = sens === "hausse" ? `+${valeur}` : sens === "baisse" ? `-${valeur}` : "stable";
  return { sens, texte: `${signe} ${valeur}`, label: `Évolution du cost : ${dit}` } as const;
}
