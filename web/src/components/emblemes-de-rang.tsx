import { useState } from "react";
import { emblemeDeRangUrl, normalizeTier, tierName } from "@/lib/ranks";
import { cn } from "@/lib/utils";

/** L'emblème officiel d'un palier ; indisponible ou Unranked, un rond neutre. */
export function EmblemeDeRang({ tier, className }: { tier: string | null; className?: string }) {
  const [absent, setAbsent] = useState(false);
  const url = emblemeDeRangUrl(tier);
  if (!url || absent) return <span aria-hidden className={cn("inline-block size-9 rounded-full bg-muted", className)} />;
  return (
    <img
      src={url}
      alt={`Emblème ${tierName(normalizeTier(tier))}`}
      className={cn("size-9 object-contain", className)}
      onError={() => setAbsent(true)}
    />
  );
}
