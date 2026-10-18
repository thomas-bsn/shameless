import { createFileRoute, Link } from "@tanstack/react-router";
import { $api, type Schemas } from "@/api/client";
import { GameClips } from "@/components/game-clips";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { PronosticsDeLaGame } from "@/components/pronostics-de-la-game";
import { VotesDeLaGame } from "@/components/votes-de-la-game";
import { dureeLabel, kdaLabel } from "@/lib/game";
import { gameWinner } from "@/lib/history";
import { sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";
import { dateHeureLabel } from "@/lib/dates";

/** La page d'une Game : c'est le lien de récap posté par le bot Discord. */
export const Route = createFileRoute("/_connecte/games/$gameId")({
  component: GamePage,
});

type Recap = Schemas["GameRecapResponse"];

function GamePage() {
  const { gameId } = Route.useParams();
  const game = $api.useQuery("get", "/games/{id}", { params: { path: { id: Number(gameId) } } });

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <Link to="/historique" className="text-sm text-muted-foreground hover:text-foreground">
        ← Historique
      </Link>
      {game.isPending ? <LoadingRows /> : game.isError ? <ErrorAlert error={game.error} /> : <Recit game={game.data} />}
    </div>
  );
}

function Recit({ game }: { game: Recap }) {
  const winner = gameWinner(game);
  const aOnBlue = game.teamASide !== "red";
  const title = game.seriesId == null ? `Game #${game.id}` : `Game ${game.gameNumber} · Série BO${game.bestOf}`;

  return (
    <>
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <p className="text-sm text-muted-foreground">{dateHeureLabel(game.playedAt)}</p>
          <h1 className="text-2xl font-bold">{title}</h1>
        </div>
        <div className="flex flex-wrap items-center gap-3">
          {game.seriesId != null && (
            <span className="font-semibold">
              Équipe A {game.teamAWins} – {game.teamBWins} Équipe B
            </span>
          )}
          {winner ? (
            <Badge variant="secondary">Équipe {winner} gagne</Badge>
          ) : (
            <Badge variant="outline">En cours</Badge>
          )}
        </div>
      </div>
      <div className="flex flex-wrap gap-4 text-sm text-muted-foreground">
        {game.match?.durationSeconds != null && <span>{dureeLabel(game.match.durationSeconds)}</span>}
        {game.seriesId != null && (
          <Link to="/series/$seriesId" params={{ seriesId: String(game.seriesId) }} className="hover:text-foreground">
            Voir la Série
          </Link>
        )}
      </div>
      <div className="grid gap-4 md:grid-cols-2">
        <Equipe
          team={aOnBlue ? "A" : "B"}
          side="blue"
          players={aOnBlue ? game.teamA : game.teamB}
          won={winner === (aOnBlue ? "A" : "B")}
        />
        <Equipe
          team={aOnBlue ? "B" : "A"}
          side="red"
          players={aOnBlue ? game.teamB : game.teamA}
          won={winner === (aOnBlue ? "B" : "A")}
        />
      </div>
      <VotesDeLaGame gameId={game.id} />
      <PronosticsDeLaGame gameId={game.id} />
      <GameClips gameId={game.id} />
    </>
  );
}

function Equipe({
  team,
  side,
  players,
  won,
}: {
  team: string;
  side: "blue" | "red";
  players: Schemas["GameHistoryParticipantDto"][];
  won: boolean;
}) {
  const label = `Équipe ${team} · ${side === "blue" ? "Blue" : "Red"} side`;

  return (
    <Card role="group" aria-label={label} className={cn(won && "border-emerald-600/50 dark:border-emerald-400/50")}>
      <CardHeader>
        <CardTitle className={cn("text-sm", sideTextClass(side))}>{label}</CardTitle>
      </CardHeader>
      <CardContent>
        <ul className="flex flex-col gap-2">
          {players.map((p) => (
            <li key={p.playerId} className="flex items-center justify-between gap-2 text-sm">
              <span className="flex min-w-0 flex-col">
                <span className="truncate font-medium">{p.prenom}</span>
                {p.championId && <span className="text-xs text-muted-foreground">{p.championId}</span>}
              </span>
              <span className="flex items-center gap-3 text-xs text-muted-foreground">
                {p.stats && <span>{kdaLabel(p.stats)}</span>}
                {p.performance && <Badge variant="outline">{p.performance.note}/100</Badge>}
              </span>
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}
