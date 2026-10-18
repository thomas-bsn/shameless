import { useMemo, useState } from "react";
import { createFileRoute, Link } from "@tanstack/react-router";
import { $api } from "@/api/client";
import { PageHeader } from "@/components/page-header";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { CostTierBadge, RankBadge, RoleIcon } from "@/components/rank";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";

export const Route = createFileRoute("/_connecte/joueurs/")({
  component: Joueurs,
});

function Joueurs() {
  const players = $api.useQuery("get", "/players");
  const ranking = $api.useQuery("get", "/classements/5v5");
  const [search, setSearch] = useState("");
  const [sort, setSort] = useState<"name" | "cost" | "role" | "rank" | "peak" | "winrate">("name");
  const winrates = new Map((ranking.data?.joueurs ?? []).map((line) => [line.playerId, line.winrate]));
  const visible = useMemo(() => [...(players.data ?? [])].filter((p) => `${p.prenom} ${p.riotId} ${p.mainRole ?? ""}`.toLocaleLowerCase("fr-FR").includes(search.toLocaleLowerCase("fr-FR"))).sort((a, b) => {
    if (sort === "cost") return b.currentCost - a.currentCost || a.prenom.localeCompare(b.prenom);
    if (sort === "role") return (a.mainRole ?? "").localeCompare(b.mainRole ?? "") || a.prenom.localeCompare(b.prenom);
    if (sort === "rank") return (b.rankTier ?? "").localeCompare(a.rankTier ?? "") || a.prenom.localeCompare(b.prenom);
    if (sort === "peak") return (b.peakTier ?? "").localeCompare(a.peakTier ?? "") || (a.peakDivision ?? 0) - (b.peakDivision ?? 0) || a.prenom.localeCompare(b.prenom);
    if (sort === "winrate") return (winrates.get(b.id) ?? -1) - (winrates.get(a.id) ?? -1) || a.prenom.localeCompare(b.prenom);
    return a.prenom.localeCompare(b.prenom);
  }), [players.data, search, sort, ranking.data]);

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
        <div className="flex flex-col gap-4"><Input aria-label="Rechercher un joueur" placeholder="Rechercher un joueur ou un rôle…" value={search} onChange={(e) => setSearch(e.target.value)} />
          <div className="overflow-x-auto rounded-xl border border-border/70 bg-card/60 backdrop-blur"><table className="w-full min-w-[680px] text-left text-sm"><thead className="border-b border-border/70 text-xs text-muted-foreground"><tr>
            <th className="p-3"><SortButton label="Joueur" value="name" sort={sort} onSort={setSort} /></th><th className="p-3"><SortButton label="Cost" value="cost" sort={sort} onSort={setSort} /></th><th className="p-3"><SortButton label="Rôle" value="role" sort={sort} onSort={setSort} /></th><th className="p-3"><SortButton label="SoloQ" value="rank" sort={sort} onSort={setSort} /></th><th className="p-3"><SortButton label="Peak" value="peak" sort={sort} onSort={setSort} /></th><th className="p-3"><SortButton label="Winrate customs" value="winrate" sort={sort} onSort={setSort} /></th>
          </tr></thead><tbody className="divide-y divide-border/50">{visible.map((p) => <tr key={p.id} className="hover:bg-muted/30"><td className="p-3"><Link to="/joueurs/$playerId" params={{ playerId: String(p.id) }} className="font-medium hover:underline">{p.prenom}</Link><div className="text-xs text-muted-foreground">{p.riotId}</div></td><td className="p-3"><CostTierBadge cost={p.currentCost} /><span className="ml-2 font-mono text-xs">{p.currentCost.toFixed(1)}</span></td><td className="p-3">{p.mainRole ? <span className="flex items-center gap-2"><RoleIcon role={p.mainRole} className="size-5" />{p.mainRole}</span> : "—"}</td><td className="p-3"><RankBadge tier={p.rankTier} division={p.rankDivision} lp={p.lp} /></td><td className="p-3 text-xs">{p.peakTier ?? "—"}{p.peakSeason ? ` · ${p.peakSeason}` : ""}</td><td className="p-3 font-mono text-xs">{winrates.get(p.id) == null ? "—" : `${Math.round(winrates.get(p.id)!)}%`}</td></tr>)}</tbody></table>{visible.length === 0 && <p className="p-8 text-center text-sm text-muted-foreground">Aucun joueur ne correspond à la recherche.</p>}</div>
        </div>
      )}
    </>
  );
}

function SortButton({ label, value, sort, onSort }: { label: string; value: "name" | "cost" | "role" | "rank" | "peak" | "winrate"; sort: string; onSort: (value: "name" | "cost" | "role" | "rank" | "peak" | "winrate") => void }) {
  return <button type="button" className="font-medium hover:text-foreground" onClick={() => onSort(value)}>{label}{sort === value ? " ↕" : ""}</button>;
}
