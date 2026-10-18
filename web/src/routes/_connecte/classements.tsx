import { createFileRoute, useNavigate } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { $api, type Schemas } from "@/api/client";
import { PageHeader } from "@/components/page-header";
import { PlayerCard } from "@/components/player-card";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { CostTierBadge, RankBadge, RankIcon } from "@/components/rank";
import { TierSection } from "@/components/tier-section";
import { Badge } from "@/components/ui/badge";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { COST_POINTS, groupByCostTier } from "@/lib/cost";
import { tierName } from "@/lib/ranks";
import { averageCost, displayRank, groupBySoloQTier, soloQLevel } from "@/lib/soloq";
import { costTierClasses, soloQTierClasses } from "@/lib/tier-styles";

type Vue = "soloq" | "cost";

export const Route = createFileRoute("/_connecte/classements")({
  validateSearch: (search: Record<string, unknown>): { vue?: Vue } => (search.vue === "cost" ? { vue: "cost" } : {}),
  component: Classements,
});

function Classements() {
  const { vue = "soloq" } = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });
  const players = $api.useQuery("get", "/players");

  return (
    <>
      <PageHeader
        title="Classements"
        description={
          (vue === "soloq" ? "Rangs SoloQ officiels, ou le peak s'il est meilleur" : "Cost des customs, pour équilibrer les équipes") +
          (players.data ? ` · ${players.data.length} joueurs` : "")
        }
        actions={players.data && (vue === "soloq" ? <SoloQLevel players={players.data} /> : <AverageCost players={players.data} />)}
      />
      <Tabs
        value={vue}
        onValueChange={(value) => navigate({ search: value === "cost" ? { vue: "cost" } : {} })}
        className="mb-5"
      >
        <TabsList>
          <TabsTrigger value="soloq">SoloQ</TabsTrigger>
          <TabsTrigger value="cost">Cost</TabsTrigger>
        </TabsList>
      </Tabs>
      {players.isPending ? (
        <LoadingRows />
      ) : players.isError ? (
        <ErrorAlert error={players.error} />
      ) : vue === "soloq" ? (
        <SoloQRanking players={players.data} />
      ) : (
        <CostRanking players={players.data} />
      )}
    </>
  );
}

function SoloQRanking({ players }: { players: Schemas["PlayerDto"][] }) {
  return (
    <div className="flex flex-col gap-3">
      {groupBySoloQTier(players).map(({ tier, players }) => (
        <TierSection
          key={tier}
          label={`Tier ${tierName(tier)}`}
          title={
            <>
              <RankIcon tier={tier} className="size-7" />
              {tierName(tier)}
            </>
          }
          count={players.length}
          className={soloQTierClasses[tier]}
        >
          {players.map((p) => {
            const rank = displayRank(p);
            return (
              <PlayerCard
                key={p.id}
                player={p}
                rank={rank}
                tag={
                  rank.isPeak && (
                    <Badge variant="outline" className="text-[10px] text-amber-600 dark:text-amber-400">
                      Peak{p.peakSeason && ` ${p.peakSeason}`}
                    </Badge>
                  )
                }
              />
            );
          })}
        </TierSection>
      ))}
    </div>
  );
}

function CostRanking({ players }: { players: Schemas["PlayerDto"][] }) {
  return (
    <div className="flex flex-col gap-3">
      {groupByCostTier(players).map(({ tier, players }) => (
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
            <PlayerCard key={p.id} player={p} showCost />
          ))}
        </TierSection>
      ))}
    </div>
  );
}

function Stat({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div role="group" aria-label={label} className="flex flex-col items-end gap-1">
      <span className="text-xs tracking-wide text-muted-foreground uppercase">{label}</span>
      {children}
    </div>
  );
}

function SoloQLevel({ players }: { players: Schemas["PlayerDto"][] }) {
  const level = soloQLevel(players);
  if (!level) return null;
  return (
    <div className="flex gap-4">
      <Stat label="Médiane">
        <RankBadge {...level.median} />
      </Stat>
      <Stat label="Moyenne">
        <RankBadge {...level.average} />
      </Stat>
    </div>
  );
}

function AverageCost({ players }: { players: Schemas["PlayerDto"][] }) {
  const average = averageCost(players);
  if (average === null) return null;
  return (
    <Stat label="Cost moyen">
      <span className="flex items-center gap-2">
        <CostTierBadge cost={average} />
        <span className="text-sm text-muted-foreground">{average.toFixed(1)} pts</span>
      </span>
    </Stat>
  );
}

