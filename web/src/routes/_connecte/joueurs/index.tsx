import { createFileRoute, Link } from "@tanstack/react-router";
import { $api } from "@/api/client";
import { PageHeader } from "@/components/page-header";
import { PlayerCard } from "@/components/player-card";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { CostTierBadge } from "@/components/rank";
import { TierSection } from "@/components/tier-section";
import { Button } from "@/components/ui/button";
import { COST_POINTS, groupByCostTier } from "@/lib/cost";
import { costTierClasses } from "@/lib/tier-styles";

export const Route = createFileRoute("/_connecte/joueurs/")({
  component: Joueurs,
});

function Joueurs() {
  const players = $api.useQuery("get", "/players");

  return (
    <>
      <PageHeader
        title="Joueurs"
        description={players.data ? `${players.data.length} joueurs, rangés par tier de cost` : undefined}
        actions={
          <Button asChild>
            <Link to="/joueurs/nouveau">Ajouter un joueur</Link>
          </Button>
        }
      />
      {players.isPending ? (
        <LoadingRows />
      ) : players.isError ? (
        <ErrorAlert error={players.error} />
      ) : (
        <div className="flex flex-col gap-3">
          {groupByCostTier(players.data).map(({ tier, players }) => (
            <TierSection
              key={tier}
              label={`Tier ${tier}`}
              title={
                <>
                  <CostTierBadge cost={COST_POINTS[tier]} className="text-base" />
                  <span className="text-sm font-normal text-muted-foreground">{COST_POINTS[tier]} pts</span>
                </>
              }
              count={players.length}
              className={costTierClasses[tier]}
            >
              {players.map((p) => (
                <PlayerCard key={p.id} player={p} />
              ))}
            </TierSection>
          ))}
        </div>
      )}
    </>
  );
}
