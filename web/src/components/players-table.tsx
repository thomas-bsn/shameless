import { Link } from "@tanstack/react-router";
import { ArrowDownIcon, ArrowUpDownIcon, ArrowUpIcon } from "lucide-react";
import { useMemo, useState } from "react";
import type { Schemas } from "@/api/client";
import { CostTierBadge, RankBadge, RoleIcon } from "@/components/rank";
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from "@/components/ui/table";
import { points } from "@/lib/cost";
import { sortPlayers, type PlayerSortKey, type SortDirection } from "@/lib/players";
import { roleLabel } from "@/lib/ranks";

const COLUMNS: { key: PlayerSortKey; label: string; firstDirection: SortDirection }[] = [
  { key: "joueur", label: "Joueur", firstDirection: "asc" },
  { key: "cost", label: "Cost", firstDirection: "desc" },
  { key: "role", label: "Rôle", firstDirection: "asc" },
  { key: "rang", label: "Rang SoloQ", firstDirection: "desc" },
  { key: "peak", label: "Peak", firstDirection: "desc" },
  { key: "winrate", label: "Winrate customs", firstDirection: "desc" },
];

/**
 * Tableau des Joueurs, trié par cost décroissant au départ ; un clic sur un en-tête trie par cette
 * colonne, un second clic inverse. `winrates` : winrate en customs (0 à 100) par id de Joueur.
 */
export function PlayersTable({
  players,
  winrates,
}: {
  players: Schemas["PlayerDto"][];
  winrates: ReadonlyMap<number, number>;
}) {
  const [sort, setSort] = useState<{ key: PlayerSortKey; direction: SortDirection }>({
    key: "cost",
    direction: "desc",
  });
  const sorted = useMemo(
    () => sortPlayers(players, sort.key, sort.direction, winrates),
    [players, sort, winrates],
  );

  function toggle(key: PlayerSortKey, firstDirection: SortDirection) {
    setSort((current) =>
      current.key === key
        ? { key, direction: current.direction === "asc" ? "desc" : "asc" }
        : { key, direction: firstDirection },
    );
  }

  return (
    <Table>
      <TableHeader>
        <TableRow>
          {COLUMNS.map(({ key, label, firstDirection }) => {
            const active = sort.key === key;
            const Icon = !active ? ArrowUpDownIcon : sort.direction === "asc" ? ArrowUpIcon : ArrowDownIcon;
            return (
              <TableHead
                key={key}
                aria-sort={active ? (sort.direction === "asc" ? "ascending" : "descending") : undefined}
              >
                <button
                  type="button"
                  onClick={() => toggle(key, firstDirection)}
                  className="inline-flex items-center gap-1 rounded-sm hover:text-foreground/80 focus-visible:ring-2 focus-visible:ring-ring focus-visible:outline-hidden"
                >
                  {label}
                  <Icon className="size-3.5 text-muted-foreground" aria-hidden />
                </button>
              </TableHead>
            );
          })}
        </TableRow>
      </TableHeader>
      <TableBody>
        {sorted.map((p) => {
          const winrate = winrates.get(p.id);
          return (
            <TableRow key={p.id}>
              <TableCell>
                <Link
                  to="/joueurs/$playerId"
                  params={{ playerId: String(p.id) }}
                  className="flex flex-col hover:underline"
                >
                  <span className="font-semibold">{p.prenom}</span>
                  <span className="text-xs text-muted-foreground">{p.riotId}</span>
                </Link>
              </TableCell>
              <TableCell>
                <span className="inline-flex items-center gap-2">
                  <CostTierBadge cost={p.currentCost} className="text-xs" />
                  <span className="font-mono tabular-nums" title={points(p.currentCost)}>
                    {Number(p.currentCost.toFixed(1))}
                  </span>
                </span>
              </TableCell>
              <TableCell>
                {roleLabel(p.mainRole) ? (
                  <span className="inline-flex items-center gap-1.5">
                    <RoleIcon role={p.mainRole} />
                    {roleLabel(p.mainRole)}
                  </span>
                ) : (
                  <span className="text-muted-foreground">—</span>
                )}
              </TableCell>
              <TableCell>
                <RankBadge tier={p.rankTier} division={p.rankDivision} lp={p.lp} />
              </TableCell>
              <TableCell>
                {p.peakTier ? (
                  <RankBadge tier={p.peakTier} division={p.peakDivision} lp={null} />
                ) : (
                  <span className="text-muted-foreground">non renseigné</span>
                )}
              </TableCell>
              <TableCell>
                {winrate === undefined ? (
                  <span className="text-muted-foreground">—</span>
                ) : (
                  <span className="inline-flex items-center gap-2">
                    <span className="w-12 font-mono tabular-nums">{Math.round(winrate)} %</span>
                    <progress
                      value={winrate}
                      max={100}
                      aria-hidden
                      className="h-1.5 w-16 overflow-hidden rounded-full [&::-moz-progress-bar]:bg-primary [&::-webkit-progress-bar]:bg-muted [&::-webkit-progress-value]:bg-primary"
                    />
                  </span>
                )}
              </TableCell>
            </TableRow>
          );
        })}
      </TableBody>
    </Table>
  );
}
