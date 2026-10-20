import type { Schemas } from "@/api/client";
import { AvatarJoueur } from "@/components/avatar-joueur";
import { Marche, PlayerLink } from "@/components/podium";
import { CostTierBadge, RoleIcon } from "@/components/rank";
import { Card } from "@/components/ui/card";
import { costText, evolutionDuCost, placeText, rankByCost, stairSteps } from "@/lib/cost";
import { type CostMode, useCostMode } from "@/lib/cost-mode";
import { costTierClasses } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";
import { useState } from "react";

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

/** Une marche du podium ; le premier est plus haut et bordé. */
function MarcheCost({ player, rang }: { player: Player; rang: number }) {
  return (
    <Marche label={`${placeText(rang)} : ${player.prenom}`} premier={rang === 1} bordure="border-amber-400/70 ring-amber-400/40">
      <span className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{placeText(rang)}</span>
      <AvatarJoueur prenom={player.prenom} avatarUrl={player.avatarUrl} className="size-10 text-lg" />
      <PlayerLink playerId={player.id} prenom={player.prenom} className="max-w-full truncate" />
      <CostTierBadge cost={player.currentCost} className="text-sm" />
      <span className={cn("font-mono font-bold tabular-nums", rang === 1 ? "text-3xl" : "text-2xl")}>{costText(player.currentCost)}</span>
      <Evolution player={player} />
    </Marche>
  );
}

const MODES: { value: CostMode; label: string }[] = [
  { value: "escalier", label: "Escalier" },
  { value: "podium", label: "Podium et liste" },
];

/** La hauteur de chaque marche, du D au SS. */
const HAUTEURS = ["min-h-8", "min-h-16", "min-h-24", "min-h-32", "min-h-40", "min-h-48"];

/** Un joueur debout sur sa marche ; au survol (ou au focus), son cost et sa tendance du mois. */
function JoueurSurMarche({ player }: { player: Player }) {
  const [survol, setSurvol] = useState(false);
  return (
    <li
      className="relative flex w-14 flex-col items-center gap-1"
      onMouseEnter={() => setSurvol(true)}
      onMouseLeave={() => setSurvol(false)}
      onFocus={() => setSurvol(true)}
      onBlur={() => setSurvol(false)}
    >
      <AvatarJoueur prenom={player.prenom} avatarUrl={player.avatarUrl} className="size-10 text-sm" />
      <PlayerLink playerId={player.id} prenom={player.prenom} className="max-w-full truncate text-xs" />
      {survol && (
        <div
          role="tooltip"
          className="absolute bottom-full z-10 mb-1 flex items-center gap-2 rounded-md border bg-popover px-2 py-1 text-xs whitespace-nowrap shadow-md"
        >
          <span className="font-mono font-bold tabular-nums">{costText(player.currentCost)}</span>
          <Evolution player={player} />
        </div>
      )}
    </li>
  );
}

/** L'escalier : six marches du D au SS, de gauche à droite, de plus en plus hautes. */
function Escalier({ players }: { players: Player[] }) {
  return (
    <ol aria-label="Escalier du cost" className="flex items-end gap-1 overflow-x-auto pt-12">
      {stairSteps(players).map(({ tier, players: debout }, i) => (
        <li key={tier} aria-label={`Marche ${tier}`} className="flex min-w-24 flex-1 flex-col justify-end">
          <ul aria-label={`Joueurs ${tier}`} className="flex flex-wrap items-end justify-center gap-2 pb-1">
            {debout.map((p) => (
              <JoueurSurMarche key={p.id} player={p} />
            ))}
          </ul>
          <div
            className={cn(
              "flex items-start justify-center rounded-t-md border pt-1 text-lg font-bold",
              HAUTEURS[i],
              costTierClasses[tier],
            )}
          >
            {tier}
          </div>
        </li>
      ))}
    </ol>
  );
}

/** Le choix du mode d'affichage, mémorisé pour l'utilisateur. */
function ChoixDuMode() {
  const [mode, setMode] = useCostMode();
  return (
    <div role="radiogroup" aria-label="Affichage du classement" className="inline-flex self-end rounded-lg border p-0.5">
      {MODES.map((m) => (
        <button
          key={m.value}
          type="button"
          role="radio"
          aria-checked={mode === m.value}
          onClick={() => setMode(m.value)}
          className={cn(
            "rounded-md px-3 py-1 text-sm",
            mode === m.value ? "bg-accent font-medium text-accent-foreground" : "text-muted-foreground hover:text-foreground",
          )}
        >
          {m.label}
        </button>
      ))}
    </div>
  );
}

/** Le classement par cost : l'escalier, ou le podium des trois premiers puis la liste des suivants. */
export function CostRanking({ players }: { players: Player[] }) {
  const [mode] = useCostMode();
  const classes = rankByCost(players);
  const [premier, deuxieme, troisieme] = classes;
  const suivants = classes.slice(3);

  if (!premier) return <p className="text-muted-foreground">Aucun joueur pour l'instant.</p>;

  if (mode === "escalier")
    return (
      <div className="flex flex-col gap-6">
        <ChoixDuMode />
        <Escalier players={players} />
      </div>
    );

  return (
    <div className="flex flex-col gap-6">
      <ChoixDuMode />
      <ol aria-label="Podium du cost" className="flex flex-col gap-3 sm:flex-row sm:items-end">
        {deuxieme && <MarcheCost player={deuxieme} rang={2} />}
        <MarcheCost player={premier} rang={1} />
        {troisieme && <MarcheCost player={troisieme} rang={3} />}
      </ol>
      {suivants.length > 0 && (
        <Card className="py-2">
          <ol aria-label="Classement du cost" className="divide-y">
            {suivants.map((p, i) => (
              <li key={p.id} aria-label={`${placeText(i + 4)} : ${p.prenom}`} className="flex items-center gap-3 px-4 py-2.5">
                <span className="w-6 text-right font-mono text-sm text-muted-foreground tabular-nums">{i + 4}</span>
                <AvatarJoueur prenom={p.prenom} avatarUrl={p.avatarUrl} className="size-8 text-sm" />
                <PlayerLink playerId={p.id} prenom={p.prenom} className="flex-1 truncate" />
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
