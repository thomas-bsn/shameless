import { Link } from "@tanstack/react-router";
import type { Schemas } from "@/api/client";
import { CostTierBadge, RoleIcon } from "@/components/rank";
import { Card } from "@/components/ui/card";
import { costText, evolutionDuCost, placeText, rankByCost } from "@/lib/cost";
import { cn } from "@/lib/utils";

type Player = Schemas["PlayerDto"];

const evolutionClasses = { hausse: "text-emerald-500", baisse: "text-red-500", stable: "text-muted-foreground" };

/** L'évolution du cost depuis le mois précédent ; rien quand elle n'est pas connue. */
function Evolution({ player }: { player: Player }) {
  const evolution = evolutionDuCost(player.costEvolution);
  if (!evolution) return null;
  return (
    <span aria-label={evolution.label} className={cn("font-mono text-xs tabular-nums", evolutionClasses[evolution.sens])}>
      {evolution.texte}
    </span>
  );
}

function PlayerLink({ player, className }: { player: Player; className?: string }) {
  return (
    <Link
      to="/joueurs/$playerId"
      params={{ playerId: String(player.id) }}
      className={cn("font-semibold hover:underline", className)}
    >
      {player.prenom}
    </Link>
  );
}

function Initiale({ player, className }: { player: Player; className: string }) {
  return (
    <span aria-hidden className={cn("flex items-center justify-center rounded-full bg-muted font-semibold uppercase", className)}>
      {player.prenom.charAt(0)}
    </span>
  );
}

/** Une marche du podium ; le premier est plus haut et bordé. */
function Marche({ player, rang }: { player: Player; rang: number }) {
  return (
    <li aria-label={`${placeText(rang)} : ${player.prenom}`} className={cn("flex flex-1 flex-col", rang === 1 && "sm:-mt-4")}>
      <Card className={cn("items-center gap-2 px-3 py-5 text-center", rang === 1 && "border-amber-400/70 py-7 ring-1 ring-amber-400/40")}>
        <span className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{placeText(rang)}</span>
        <Initiale player={player} className="size-10 text-lg" />
        <PlayerLink player={player} className="max-w-full truncate" />
        <CostTierBadge cost={player.currentCost} className="text-sm" />
        <span className={cn("font-mono font-bold tabular-nums", rang === 1 ? "text-3xl" : "text-2xl")}>
          {costText(player.currentCost)}
        </span>
        <Evolution player={player} />
      </Card>
    </li>
  );
}

/** Le classement par cost : podium des trois premiers, puis la liste des suivants. */
export function CostRanking({ players }: { players: Player[] }) {
  const classes = rankByCost(players);
  const [premier, deuxieme, troisieme] = classes;
  const suivants = classes.slice(3);

  if (!premier) return <p className="text-muted-foreground">Aucun joueur pour l'instant.</p>;

  return (
    <div className="flex flex-col gap-6">
      <ol aria-label="Podium du cost" className="flex flex-col gap-3 sm:flex-row sm:items-end">
        {deuxieme && <Marche player={deuxieme} rang={2} />}
        <Marche player={premier} rang={1} />
        {troisieme && <Marche player={troisieme} rang={3} />}
      </ol>
      {suivants.length > 0 && (
        <Card className="py-2">
          <ol aria-label="Classement du cost" className="divide-y">
            {suivants.map((p, i) => (
              <li key={p.id} aria-label={`${placeText(i + 4)} : ${p.prenom}`} className="flex items-center gap-3 px-4 py-2.5">
                <span className="w-6 text-right font-mono text-sm text-muted-foreground tabular-nums">{i + 4}</span>
                <Initiale player={p} className="size-8 text-sm" />
                <PlayerLink player={p} className="flex-1 truncate" />
                <RoleIcon role={p.mainRole} />
                <Evolution player={p} />
                <CostTierBadge cost={p.currentCost} className="text-xs" />
                <span className="w-10 text-right font-mono font-semibold tabular-nums">{costText(p.currentCost)}</span>
              </li>
            ))}
          </ol>
        </Card>
      )}
    </div>
  );
}
