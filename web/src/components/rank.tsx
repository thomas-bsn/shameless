import { costTier } from "@/lib/cost";
import { normalizeTier, rankLabel, roleLabel } from "@/lib/ranks";
import { costTierClasses, soloQTierClasses } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

export function RankIcon({ tier, className }: { tier: string | null; className?: string }) {
  return <img src={`/ranks/${normalizeTier(tier).toLowerCase()}.svg`} alt="" className={cn("size-5", className)} />;
}

/** Rang SoloQ (ou peak) avec l'icône et la couleur de son tier. */
export function RankBadge({
  tier,
  division,
  lp,
  className,
}: {
  tier: string | null;
  division: number | null;
  lp: number | null;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-md border px-2 py-0.5 text-xs font-semibold",
        soloQTierClasses[normalizeTier(tier)],
        className,
      )}
    >
      <RankIcon tier={tier} className="size-4" />
      {rankLabel(tier, division, lp)}
    </span>
  );
}

export function CostTierBadge({ cost, className }: { cost: number; className?: string }) {
  const tier = costTier(cost);
  return (
    <span className={cn("inline-flex min-w-8 justify-center rounded-md border px-1.5 font-bold", costTierClasses[tier], className)}>
      {tier}
    </span>
  );
}

export function RoleIcon({ role, className }: { role: string | null; className?: string }) {
  const label = roleLabel(role);
  if (!label || !role) return null;
  return <img src={`/roles/${role.toLowerCase()}.png`} alt={label} title={label} className={cn("size-4 opacity-70", className)} />;
}
