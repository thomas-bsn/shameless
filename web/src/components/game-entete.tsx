import { Link, useNavigate } from "@tanstack/react-router";
import type { Schemas } from "@/api/client";
import type { Participant, Side } from "@/components/game-equipe-table";
import { sidesOf } from "@/components/game-scoreboard";
import { IconeChampion } from "@/components/icones-de-jeu/icones-catalogue";
import { BoutonModifierLaGame } from "@/components/modifier-game/bouton-modifier-la-game";
import { Badge } from "@/components/ui/badge";
import { dateHeureLabel, dateLabel } from "@/lib/dates";
import { dureeLabel, sommeConnue } from "@/lib/game";
import { gameWinner } from "@/lib/history";
import { sideBorderClass, sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

type Recap = Schemas["GameRecapResponse"];

/** Le total de kills d'une équipe, somme de ses joueurs ; null dès qu'un joueur n'a pas ses stats. */
function killsDeLEquipe(joueurs: Participant[]): number | null {
  const kills = joueurs.map((p) => p.stats?.kills);
  return kills.some((k) => k == null) ? null : sommeConnue(kills);
}

function Equipe({
  team,
  side,
  joueurs,
  issue,
}: {
  team: string;
  side: Side;
  joueurs: Participant[];
  issue: "Victoire" | "Défaite" | null;
}) {
  const droite = side === "red";
  return (
    <div
      role="group"
      aria-label={`Équipe ${team} (${side === "blue" ? "côté bleu" : "côté rouge"})`}
      className={cn("relative flex flex-col gap-2", droite && "md:items-end")}
    >
      <div className={cn("flex flex-wrap items-center gap-2", droite && "md:flex-row-reverse")}>
        <span className={cn("text-xl font-bold", sideTextClass(side))}>Équipe {team}</span>
        {issue && (
          <Badge
            variant="outline"
            className={cn(
              "uppercase",
              issue === "Victoire"
                ? "border-green-400/30 bg-green-400/15 text-green-300"
                : "border-red-400/30 bg-red-400/15 text-red-300",
            )}
          >
            {issue}
          </Badge>
        )}
      </div>
      <div className={cn("flex gap-1.5", droite && "md:flex-row-reverse")}>
        {joueurs.map((p) => (
          <IconeChampion
            key={p.playerId}
            championId={p.championId}
            className={cn("size-9 rounded-lg border-2", sideBorderClass(side))}
          />
        ))}
      </div>
    </div>
  );
}

/** L'en-tête de la page d'une game : le duel des deux équipes, bleue à gauche et rouge à droite. */
export function EnteteDeGame({ game }: { game: Recap }) {
  const navigate = useNavigate();
  const winner = gameWinner(game);
  const [gauche, droite] = sidesOf(game);
  const killsGauche = killsDeLEquipe(gauche.players);
  const killsDroite = killsDeLEquipe(droite.players);
  const duree = game.match?.durationSeconds != null ? dureeLabel(game.match.durationSeconds) : null;
  const victoiresDeSerie = (team: string) => (team === "A" ? game.teamAWins : game.teamBWins);
  const meta =
    game.seriesId == null
      ? `Game #${game.id}`
      : `Game ${game.gameNumber} · BO${game.bestOf} · ${victoiresDeSerie(gauche.team)} – ${victoiresDeSerie(droite.team)}`;
  const titre = winner
    ? [`Victoire de l'Équipe ${winner}`, duree].filter(Boolean).join(" · ")
    : `Game ${game.gameNumber ?? `#${game.id}`}`;
  const issue = (team: string) => (winner ? (winner === team ? "Victoire" : "Défaite") : null);

  return (
    <section
      aria-label="En-tête de la game"
      className="relative flex flex-col gap-4 overflow-hidden rounded-2xl border bg-card p-5"
    >
      <div aria-hidden className="absolute inset-y-0 left-0 w-[45%] bg-sky-400/5" />
      <div aria-hidden className="absolute inset-y-0 right-0 w-[45%] bg-red-400/5" />
      <h1 className="sr-only">{titre}</h1>
      <div className="relative grid items-center gap-6 md:grid-cols-[1fr_auto_1fr]">
        <Equipe team={gauche.team} side="blue" joueurs={gauche.players} issue={issue(gauche.team)} />
        <div role="group" aria-label="Score" className="flex flex-col items-center gap-1 text-center">
          {killsGauche != null && killsDroite != null && (
            <p className="font-mono text-4xl font-bold tabular-nums">
              {killsGauche}
              <span className="text-muted-foreground"> : </span>
              {killsDroite}
            </p>
          )}
          {!winner && <Badge variant="outline">En cours</Badge>}
          <p className="text-xs text-muted-foreground">{[duree, meta].filter(Boolean).join(" · ")}</p>
          <p className="text-xs text-muted-foreground">{dateHeureLabel(game.playedAt)}</p>
          {game.modifieeParAdminLe && (
            <Badge variant="secondary">modifiée par un admin le {dateLabel(game.modifieeParAdminLe)}</Badge>
          )}
        </div>
        <Equipe team={droite.team} side="red" joueurs={droite.players} issue={issue(droite.team)} />
      </div>
      <div className="relative flex flex-wrap items-center justify-center gap-3">
        <BoutonModifierLaGame gameId={game.id} onSupprimee={() => void navigate({ to: "/historique" })} />
        {game.seriesId != null && (
          <Link
            to="/series/$seriesId"
            params={{ seriesId: String(game.seriesId) }}
            className="text-sm font-medium underline-offset-4 hover:underline"
          >
            Voir la Série
          </Link>
        )}
      </div>
    </section>
  );
}
