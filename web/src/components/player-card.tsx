import { Link } from "@tanstack/react-router";
import type { Schemas } from "@/api/client";
import { CostTierBadge, RankBadge, RoleIcon } from "@/components/rank";

/** Carte d'un Joueur dans une liste, vers sa fiche. */
export function PlayerCard({ player, showCost = false }: { player: Schemas["PlayerDto"]; showCost?: boolean }) {
  return (
    <Link
      to="/joueurs/$playerId"
      params={{ playerId: String(player.id) }}
      className="flex w-56 flex-col gap-1.5 rounded-lg border bg-card p-3 transition-colors hover:bg-accent"
    >
      <span className="flex items-center gap-2">
        {showCost && <CostTierBadge cost={player.currentCost} className="text-xs" />}
        <span className="flex-1 truncate font-semibold">{player.prenom}</span>
        <RoleIcon role={player.mainRole} />
      </span>
      <span className="truncate text-xs text-muted-foreground">{player.riotId}</span>
      <RankBadge tier={player.rankTier} division={player.rankDivision} lp={player.lp} className="self-start" />
    </Link>
  );
}
