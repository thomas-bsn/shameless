import { createFileRoute, Link } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { $api } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { CostTierBadge, RankBadge, RoleIcon } from "@/components/rank";
import { TitresDuJoueur } from "@/components/titres-du-joueur";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { roleLabel } from "@/lib/ranks";

export const Route = createFileRoute("/_connecte/joueurs/$playerId")({
  component: FicheJoueur,
});

function FicheJoueur() {
  const { playerId } = Route.useParams();
  const player = $api.useQuery("get", "/players/{playerId}", { params: { path: { playerId: Number(playerId) } } });
  const fiche = $api.useQuery("get", "/players/{playerId}/fiche", { params: { path: { playerId: Number(playerId) } } });
  const costs = $api.useQuery("get", "/players/{playerId}/cost/historique", { params: { path: { playerId: Number(playerId) } } });

  return (
    <div className="flex flex-col gap-6">
      <Link to="/joueurs" className="text-sm text-muted-foreground hover:text-foreground">
        ← Joueurs
      </Link>
      {player.isPending ? (
        <LoadingRows />
      ) : player.isError ? (
        <ErrorAlert error={player.error} />
      ) : (
        <>
          <div>
            <h1 className="text-2xl font-bold">{player.data.prenom}</h1>
            <p className="text-muted-foreground">{player.data.riotId}</p>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <Stat title="Rang SoloQ">
              <RankBadge tier={player.data.rankTier} division={player.data.rankDivision} lp={player.data.lp} />
            </Stat>
            <Stat title="Peak">
              {player.data.peakTier ? (
                <div className="flex flex-col items-start gap-1">
                  <RankBadge tier={player.data.peakTier} division={player.data.peakDivision} lp={player.data.peakLp} />
                  {player.data.peakSeason && (
                    <span className="text-xs text-muted-foreground">{player.data.peakSeason}</span>
                  )}
                </div>
              ) : (
                <span className="text-muted-foreground">Non renseigné</span>
              )}
            </Stat>
            <Stat title="Cost">
              <div className="flex items-center gap-2">
                <CostTierBadge cost={player.data.currentCost} className="text-lg" />
                <span className="text-sm text-muted-foreground">{player.data.currentCost} pts</span>
              </div>
            </Stat>
            <Stat title="Rôle principal">
              {roleLabel(player.data.mainRole) ? (
                <span className="flex items-center gap-2">
                  <RoleIcon role={player.data.mainRole} />
                  {roleLabel(player.data.mainRole)}
                </span>
              ) : (
                <span className="text-muted-foreground">Inconnu</span>
              )}
            </Stat>
            <Stat title="Rang Flex"><RankBadge tier={player.data.flexRankTier} division={player.data.flexRankDivision} lp={player.data.flexLp} /></Stat>
          </div>
          <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
            <TitresDuJoueur playerId={player.data.id} />
          </div>
          {fiche.isPending ? <LoadingRows /> : fiche.isError ? <ErrorAlert error={fiche.error} /> : <>
            <section className="flex flex-col gap-3"><h2 className="text-lg font-semibold">Stats en customs</h2><div className="grid gap-3 sm:grid-cols-2 lg:grid-cols-4">{[["Games", fiche.data.stats.gamesJouees], ["Winrate", pct(fiche.data.stats.winrate)], ["KDA", n(fiche.data.stats.kda)], ["CS/min", n(fiche.data.stats.csParMinute)], ["Participation", pct(fiche.data.stats.participationAuxKills)], ["Dégâts moyens", n(fiche.data.stats.degatsMoyens)], ["Dégâts/min", n(fiche.data.stats.degatsParMinute)], ["Note moyenne", fiche.data.stats.noteMoyenne == null ? "—" : `${fiche.data.stats.noteMoyenne.toFixed(1)}/100`]].map(([label, value]) => <Stat key={String(label)} title={String(label)}><span className="font-mono text-xl">{value}</span></Stat>)}</div></section>
            {costs.data && <section className="flex flex-col gap-3"><h2 className="text-lg font-semibold">Évolution du cost</h2><CostChart costs={costs.data.mois} /></section>}
            <div className="grid gap-4 lg:grid-cols-2"><section className="flex flex-col gap-3"><h2 className="text-lg font-semibold">Champions</h2><Rows items={fiche.data.champions.map((x) => [x.championId, `${x.stats.gamesJouees} games · ${pct(x.stats.winrate)} winrate`])} empty="Aucune game avec champion détecté." /></section><section className="flex flex-col gap-3"><h2 className="text-lg font-semibold">Rôles</h2><Rows items={fiche.data.roles.map((x) => [roleLabel(x.role) ?? String(x.role), `${x.stats.gamesJouees} games · ${pct(x.stats.winrate)} winrate`])} empty="Aucun rôle connu." /></section></div>
            <section className="flex flex-col gap-3"><h2 className="text-lg font-semibold">Dernières games</h2><div className="divide-y rounded-xl border border-border/60 bg-card/50 px-4">{fiche.data.games.length ? fiche.data.games.slice(0, 20).map((g) => <Link key={g.gameId} to="/games/$gameId" params={{ gameId: String(g.gameId) }} className="flex flex-wrap items-center gap-3 py-3 text-sm hover:underline"><span>{new Date(g.playedAt).toLocaleDateString("fr-FR")}</span><span>Game #{g.gameId}</span><span>{g.championId ?? "Champion inconnu"}</span><span className={g.victoire ? "text-emerald-600" : "text-rose-500"}>{g.victoire ? "Victoire" : "Défaite"}</span><span className="ml-auto font-mono">{g.performance ? `${g.performance.note}/100` : "—"}</span></Link>) : <p className="py-5 text-sm text-muted-foreground">Aucune game terminée.</p>}</div></section>
          </>}
        </>
      )}
    </div>
  );
}

const n = (value: number | null) => value == null ? "—" : value.toFixed(1);
const pct = (value: number | null) => value == null ? "—" : `${Math.round(value * 100)}%`;

function Rows({ items, empty }: { items: [string, string][]; empty: string }) {
  return <div className="divide-y rounded-xl border border-border/60 bg-card/50 px-4">{items.length ? items.map(([label, value]) => <p key={label} className="flex justify-between gap-3 py-3 text-sm"><span>{label}</span><span className="text-muted-foreground">{value}</span></p>) : <p className="py-5 text-sm text-muted-foreground">{empty}</p>}</div>;
}

function CostChart({ costs }: { costs: { mois: string; cost: number }[] }) {
  if (!costs.length) return <p className="text-sm text-muted-foreground">Aucun historique de cost.</p>;
  const width = 640, height = 180, pad = 20;
  const points = costs.map((item, index) => `${pad + index * (width - pad * 2) / Math.max(1, costs.length - 1)},${height - pad - (item.cost - 1) / 5 * (height - pad * 2)}`).join(" ");
  return <div className="rounded-xl border border-border/60 bg-card/50 p-4"><svg viewBox={`0 0 ${width} ${height}`} role="img" aria-label="Évolution mensuelle du cost de 1 à 6" className="w-full"><line x1={pad} x2={width - pad} y1={pad} y2={pad} stroke="currentColor" opacity=".15" /><line x1={pad} x2={width - pad} y1={height - pad} y2={height - pad} stroke="currentColor" opacity=".15" /><polyline points={points} fill="none" stroke="var(--primary)" strokeWidth="4" strokeLinejoin="round" strokeLinecap="round" />{costs.map((item, index) => { const x = pad + index * (width - pad * 2) / Math.max(1, costs.length - 1); const y = height - pad - (item.cost - 1) / 5 * (height - pad * 2); return <circle key={item.mois} cx={x} cy={y} r="5" fill="var(--primary)"><title>{item.mois}: {item.cost.toFixed(1)}</title></circle>; })}</svg><div className="flex justify-between text-xs text-muted-foreground">{costs[0]!.mois}<span>1 à 6</span>{costs.at(-1)!.mois}</div></div>;
}

function Stat({ title, children }: { title: string; children: ReactNode }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle className="text-sm text-muted-foreground">{title}</CardTitle>
      </CardHeader>
      <CardContent>{children}</CardContent>
    </Card>
  );
}
