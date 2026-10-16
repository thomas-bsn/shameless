import { createFileRoute, Link } from "@tanstack/react-router";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { RankBadge } from "@/components/rank";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { gameWinner, type Month, monthLabel, seriesScore, shiftMonth } from "@/lib/history";
import { outcomeLabel } from "@/lib/series";
import { sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

const currentMonth = (): Month => {
  const now = new Date();
  return { year: now.getFullYear(), month: now.getMonth() + 1 };
};

const isMonth = (year: unknown, month: unknown) =>
  Number.isInteger(year) && Number.isInteger(month) && (month as number) >= 1 && (month as number) <= 12;

export const Route = createFileRoute("/_connecte/historique")({
  validateSearch: (search: Record<string, unknown>): { annee?: number; mois?: number } =>
    isMonth(search.annee, search.mois) ? { annee: search.annee as number, mois: search.mois as number } : {},
  component: Historique,
});

const toSearch = ({ year, month }: Month) => ({ annee: year, mois: month });

function Historique() {
  const search = Route.useSearch();
  const shown: Month = search.annee && search.mois ? { year: search.annee, month: search.mois } : currentMonth();
  const previous = shiftMonth(shown, -1);
  const next = shiftMonth(shown, 1);
  const now = currentMonth();
  const hasNext = next.year * 12 + next.month <= now.year * 12 + now.month;

  const history = $api.useQuery("get", "/games/history/{year}/{month}", {
    params: { path: { year: shown.year, month: shown.month } },
  });

  const series = history.data?.games ?? [];
  const gameCount = series.reduce((sum, s) => sum + s.games.length, 0);

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <p className="text-sm text-muted-foreground">Historique</p>
          <h1 className="text-2xl font-bold">{monthLabel(shown)}</h1>
        </div>
        <nav aria-label="Mois" className="flex items-center gap-4 text-sm">
          <Link to="." search={toSearch(previous)} className="text-muted-foreground hover:text-foreground">
            ← {monthLabel(previous)}
          </Link>
          {hasNext && (
            <Link to="." search={toSearch(next)} className="text-muted-foreground hover:text-foreground">
              {monthLabel(next)} →
            </Link>
          )}
        </nav>
      </div>
      {history.isPending ? (
        <LoadingRows />
      ) : history.isError ? (
        <ErrorAlert error={history.error} />
      ) : series.length === 0 ? (
        <p className="py-12 text-center text-muted-foreground">Aucune game ce mois-ci.</p>
      ) : (
        <>
          <p className="text-sm text-muted-foreground">
            {series.length} série{series.length > 1 ? "s" : ""} · {gameCount} game{gameCount > 1 ? "s" : ""}
          </p>
          {series.map((s) => (
            <SeriesCard key={s.seriesId} series={s} />
          ))}
        </>
      )}
    </div>
  );
}

const dateLabel = (iso: string) =>
  new Date(iso).toLocaleDateString("fr-FR", { weekday: "short", day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" });

function SeriesCard({ series }: { series: Schemas["SeriesHistoryResponse"] }) {
  // seriesId 0 : games enregistrées sans Série.
  const title = series.seriesId === 0 ? "Games hors série" : `Série BO${series.format}`;
  const score = seriesScore(series.games);

  return (
    <article aria-label={title}>
      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2">
          <CardTitle>
            {title} <span className="ml-2 text-sm font-normal text-muted-foreground">{dateLabel(series.playedAt)}</span>
          </CardTitle>
          {series.seriesId !== 0 && (
            <div className="flex items-center gap-3">
              <span className="font-semibold">
                Équipe A {score.a} – {score.b} Équipe B
              </span>
              {series.outcome && (
                <Badge variant="secondary">
                  {outcomeLabel({ outcome: series.outcome, winningTeam: series.winningTeam })}
                </Badge>
              )}
            </div>
          )}
        </CardHeader>
        <CardContent>
          <ol className="flex flex-col gap-4">
            {series.games.map((g, index) => (
              <GameRow key={g.id} game={g} index={index + 1} />
            ))}
          </ol>
        </CardContent>
      </Card>
    </article>
  );
}

function GameRow({ game, index }: { game: Schemas["GameHistoryResponse"]; index: number }) {
  const aOnBlue = game.teamASide !== "red";
  const winner = gameWinner(game);

  return (
    <li className="overflow-hidden rounded-lg border">
      <div className="flex items-center justify-between gap-2 border-b bg-muted/30 px-3 py-2 text-xs text-muted-foreground">
        <span>
          Game {index} · #{game.id} · {dateLabel(game.playedAt)}
        </span>
        {winner ? (
          <Badge variant="secondary">Équipe {winner} gagne</Badge>
        ) : (
          <Badge variant="outline">En attente</Badge>
        )}
      </div>
      <div className="grid grid-cols-2 divide-x">
        <TeamColumn
          team={aOnBlue ? "A" : "B"}
          side="blue"
          players={aOnBlue ? game.teamA : game.teamB}
          won={winner === (aOnBlue ? "A" : "B")}
        />
        <TeamColumn
          team={aOnBlue ? "B" : "A"}
          side="red"
          players={aOnBlue ? game.teamB : game.teamA}
          won={winner === (aOnBlue ? "B" : "A")}
        />
      </div>
    </li>
  );
}

function TeamColumn({
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
  const cost = players.reduce((sum, p) => sum + (p.cost ?? 0), 0);

  return (
    <div role="group" aria-label={label} className={cn("flex flex-col gap-2 p-3", won && "bg-emerald-400/5")}>
      <div className="flex items-center justify-between text-xs font-semibold">
        <span className={sideTextClass(side)}>{label}</span>
        <span className="text-muted-foreground">{cost} pts</span>
      </div>
      {players.map((p) => (
        <div key={p.riotId} className="flex items-center justify-between gap-2 text-sm">
          <span className="truncate">{p.prenom}</span>
          <RankBadge tier={p.rankTier} division={p.rankDivision} lp={null} className="text-[10px]" />
        </div>
      ))}
    </div>
  );
}
