import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
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

type Vue = "soloq" | "cost" | "dofd";

export const Route = createFileRoute("/_connecte/classements")({
  validateSearch: (search: Record<string, unknown>): { vue?: Vue } => search.vue === "cost" || search.vue === "dofd" ? { vue: search.vue } : {},
  component: Classements,
});

function Classements() {
  const { vue = "soloq" } = Route.useSearch();
  const navigate = useNavigate({ from: Route.fullPath });
  const players = $api.useQuery("get", "/players");
  const hall = $api.useQuery("get", "/hall-de-la-honte");

  return (
    <>
      <PageHeader
        title="Classements"
        description={
          (vue === "soloq" ? "Rangs SoloQ officiels, ou le peak s'il est meilleur" : vue === "cost" ? "Cost des customs, pour équilibrer les équipes" : "Titres de DOFD attribués par les votes clos") +
          (players.data ? ` · ${players.data.length} joueurs` : "")
        }
        actions={players.data && (vue === "soloq" ? <SoloQLevel players={players.data} /> : vue === "cost" ? <AverageCost players={players.data} /> : null)}
      />
      <Tabs
        value={vue}
        onValueChange={(value) => navigate({ search: value === "soloq" ? {} : { vue: value as Vue } })}
        className="mb-5"
      >
        <TabsList>
          <TabsTrigger value="soloq">SoloQ</TabsTrigger>
          <TabsTrigger value="cost">Cost</TabsTrigger>
          <TabsTrigger value="dofd">DOFD</TabsTrigger>
        </TabsList>
      </Tabs>
      {players.isPending ? (
        <LoadingRows />
      ) : players.isError ? (
        <ErrorAlert error={players.error} />
      ) : vue === "soloq" ? (
        <SoloQRanking players={players.data} />
      ) : vue === "cost" ? (
        <CostRanking players={players.data} />
      ) : hall.isPending ? <LoadingRows /> : hall.isError ? <ErrorAlert error={hall.error} /> : <DofdRanking players={hall.data.joueurs} />}
    </>
  );
}

function DofdRanking({ players }: { players: Schemas["LigneHallDeLaHonte"][] }) {
  if (!players.length) return <p className="py-12 text-center text-muted-foreground">Aucun DOFD élu pour l'instant.</p>;
  const podium = players.slice(0, 3);
  return <div className="flex flex-col gap-5"><div className="grid gap-3 sm:grid-cols-3">{podium.map((p, i) => <div key={p.playerId} className="rounded-xl border border-border/70 bg-card/60 p-5 text-center backdrop-blur"><div className="text-3xl" aria-hidden="true">{["🥇", "🥈", "🥉"][i]}</div><span role="img" aria-label="Bouse du DOFD" className="my-2 block text-4xl">💩</span><LinkPlayer playerId={p.playerId} name={p.prenom} /><p className="mt-2 font-mono text-lg">{p.dofd} DOFD</p></div>)}</div><ol aria-label="Classement DOFD" className="divide-y rounded-xl border border-border/70 bg-card/60 px-4 backdrop-blur">{players.map((p) => <li key={p.playerId} className="flex items-center gap-4 py-3"><span className="w-8 text-right font-mono text-muted-foreground">{p.rang}</span><LinkPlayer playerId={p.playerId} name={p.prenom} /><span className="ml-auto font-mono">{p.dofd}</span></li>)}</ol></div>;
}

function LinkPlayer({ playerId, name }: { playerId: number; name: string }) {
  return <Link to="/joueurs/$playerId" params={{ playerId: String(playerId) }} className="font-medium hover:underline">{name}</Link>;
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
  const podium = [...players].sort((a, b) => b.currentCost - a.currentCost).slice(0, 3);
  return (
    <div className="flex flex-col gap-5">
      <section aria-label="Podium Cost" className="grid gap-3 sm:grid-cols-3">{podium.map((p, i) => <div key={p.id} className="rounded-xl border border-border/70 bg-card/60 p-4 text-center backdrop-blur"><span className="text-2xl">{["🥇", "🥈", "🥉"][i]}</span><PlayerCard player={p} showCost tag={<CostMovement playerId={p.id} />} /></div>)}</section>
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
              <PlayerCard key={p.id} player={p} showCost tag={<CostMovement playerId={p.id} />} />
          ))}
        </TierSection>
      ))}
    </div>
  );
}

function CostMovement({ playerId }: { playerId: number }) {
  const history = $api.useQuery("get", "/players/{playerId}/cost/historique", { params: { path: { playerId } } });
  const months = history.data?.mois ?? [];
  const delta = months.at(-1)?.evolution;
  if (delta == null) return <span className="text-[10px] text-muted-foreground">—</span>;
  return <span role="img" aria-label={`Évolution du cost ${delta > 0 ? "plus" : delta < 0 ? "moins" : "stable"} ${Math.abs(delta).toFixed(1)}`} className={delta > 0 ? "text-xs text-emerald-600" : delta < 0 ? "text-xs text-rose-500" : "text-xs text-muted-foreground"}>{delta > 0 ? "↑" : delta < 0 ? "↓" : "→"}{delta !== 0 && ` ${Math.abs(delta).toFixed(1)}`}</span>;
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

