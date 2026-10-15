/** Tiers SoloQ du plus haut au plus bas. */
export const SOLOQ_TIERS = [
  "CHALLENGER",
  "GRANDMASTER",
  "MASTER",
  "DIAMOND",
  "EMERALD",
  "PLATINUM",
  "GOLD",
  "SILVER",
  "BRONZE",
  "IRON",
  "UNRANKED",
] as const;

export type SoloQTier = (typeof SOLOQ_TIERS)[number];

const DIVISIONS = ["I", "II", "III", "IV"];

export const isMasterPlus = (tier: string) => ["MASTER", "GRANDMASTER", "CHALLENGER"].includes(tier);

/** « Gold », « Grandmaster »… */
export const tierName = (tier: string) => tier.charAt(0).toUpperCase() + tier.slice(1).toLowerCase();

export function normalizeTier(tier: string | null | undefined): SoloQTier {
  const upper = tier?.toUpperCase();
  return (SOLOQ_TIERS as readonly string[]).includes(upper ?? "") ? (upper as SoloQTier) : "UNRANKED";
}

/** « Gold II · 40 LP », « Master · 120 LP », « Unranked » ; sans LP connus, « Gold II ». */
export function rankLabel(tier: string | null, division: number | null, lp: number | null): string {
  const normalized = normalizeTier(tier);
  if (normalized === "UNRANKED") return "Unranked";
  const roman = !isMasterPlus(normalized) && division ? DIVISIONS[division - 1] : undefined;
  return `${tierName(normalized)}${roman ? ` ${roman}` : ""}${lp === null ? "" : ` · ${lp} LP`}`;
}

const ROLES: Record<string, string> = {
  TOP: "Top",
  JUNGLE: "Jungle",
  MIDDLE: "Mid",
  BOTTOM: "ADC",
  UTILITY: "Support",
};

/** Poste Riot (teamPosition) tel que les joueurs le disent. */
export const roleLabel = (role: string | null | undefined): string | null =>
  (role && ROLES[role.toUpperCase()]) || null;
