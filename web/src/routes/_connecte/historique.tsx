import { createFileRoute, Link } from "@tanstack/react-router";
import { ChevronLeft, ChevronRight } from "lucide-react";
import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { RankBadge } from "@/components/rank";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { calendarCells, daysPlayed, gameWinner, type Month, monthLabel, seriesScore, shiftMonth } from "@/lib/history";
import { outcomeLabel } from "@/lib/series";
import { sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";
import { dateHeureLabel } from "@/lib/dates";
import { GameLinkCard } from "@/components/game-link-card";
import { AvatarJoueur } from "@/components/avatar-joueur";

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

  return (
    <div className="flex max-w-5xl flex-col gap-6">
      <div className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <p className="text-sm text-muted-foreground">Historique</p>
          <h1 className="text-2xl font-bold">{monthLabel(shown)}</h1>
        </div>
        <nav aria-label="Mois" className="flex items-center gap-2">
          <Button asChild variant="outline" size="icon" className="rounded-full">
            <Link to="." search={toSearch(previous)} aria-label="Mois précédent">
              <ChevronLeft />
            </Link>
          </Button>
          {hasNext ? (
            <Button asChild variant="outline" size="icon" className="rounded-full">
              <Link to="." search={toSearch(next)} aria-label="Mois suivant">
                <ChevronRight />
              </Link>
            </Button>
          ) : (
            <Button variant="outline" size="icon" className="rounded-full" disabled aria-label="Mois suivant">
              <ChevronRight />
            </Button>
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
        <MonthView key={`${shown.year}-${shown.month}`} month={shown} series={series} />
      )}
    </div>
  );
}

const WEEKDAYS = ["L", "M", "M", "J", "V", "S", "D"];

function MonthView({ month, series }: { month: Month; series: Schemas["SeriesHistoryResponse"][] }) {
  const days = daysPlayed(series);
  const lastDay = Math.max(...days.keys());
  const [picked, setPicked] = useState<number | null>(null);
  const selected = picked !== null && days.has(picked) ? picked : lastDay;
  const day = days.get(selected)!;
  const dayLabel = (d: number) =>
    new Date(month.year, month.month - 1, d).toLocaleDateString("fr-FR", { day: "numeric", month: "long" });
  const dayTitle = new Date(month.year, month.month - 1, selected).toLocaleDateString("fr-FR", {
    weekday: "long",
    day: "numeric",
    month: "long",
  });
  const realSeries = day.series.filter((g) => g.series.seriesId !== 0).length;

  return (
    <div className="grid items-start gap-6 md:grid-cols-[minmax(0,22rem)_1fr]">
      <Card>
        <CardContent>
          <div
            role="group"
            aria-label={`Calendrier d'${monthLabel(month).toLowerCase()}`}
            className="grid grid-cols-7 gap-1.5"
          >
            {WEEKDAYS.map((w, i) => (
              <span key={i} aria-hidden className="pb-1 text-center text-xs text-muted-foreground">
                {w}
              </span>
            ))}
            {calendarCells(month).map((d, i) => {
              if (d === null) return <span key={`vide-${i}`} />;
              const played = days.get(d);
              if (!played) {
                return (
                  <span
                    key={d}
                    className="flex aspect-square items-center justify-center rounded-md text-sm text-muted-foreground"
                  >
                    {d}
                  </span>
                );
              }
              return (
                <button
                  key={d}
                  type="button"
                  aria-pressed={d === selected}
                  aria-label={`${dayLabel(d)}, ${played.count} game${played.count > 1 ? "s" : ""}`}
                  onClick={() => setPicked(d)}
                  className={cn(
                    "flex aspect-square flex-col items-center justify-center rounded-md border text-sm font-medium transition-colors",
                    d === selected ? "border-primary bg-primary text-primary-foreground" : "bg-muted hover:bg-accent",
                  )}
                >
                  {d}
                  <span className="font-mono text-[10px] tabular-nums opacity-80">{played.count}</span>
                </button>
              );
            })}
          </div>
        </CardContent>
      </Card>
      <section aria-label={dayTitle} className="flex flex-col gap-4">
        <div>
          <h2 className="text-lg font-semibold capitalize">{dayTitle}</h2>
          <p className="text-sm text-muted-foreground">
            {realSeries > 0 && `${realSeries} série${realSeries > 1 ? "s" : ""} · `}
            {day.count} game{day.count > 1 ? "s" : ""}
          </p>
        </div>
        {day.series.map(({ series: s, games }) => (
          <SeriesCard key={s.seriesId} series={s} games={games} />
        ))}
      </section>
    </div>
  );
}

function SeriesCard({
  series,
  games,
}: {
  series: Schemas["SeriesHistoryResponse"];
  games: Schemas["GameHistoryResponse"][];
}) {
  // seriesId 0 : games enregistrées sans Série.
  const title = series.seriesId === 0 ? "Games hors série" : `Série BO${series.format}`;
  const score = seriesScore(series.games);

  return (
    <article aria-label={title}>
      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2">
          <CardTitle>
            {title} <span className="ml-2 text-sm font-normal text-muted-foreground">{dateHeureLabel(series.playedAt)}</span>
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
            {games.map((g) => (
              <GameRow key={g.id} game={g} index={series.games.indexOf(g) + 1} />
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
    <li>
      <GameLinkCard
        gameId={game.id}
        label={`Game ${index} · #${game.id}`}
        header={
          <>
            Game {index} · #{game.id} · {dateHeureLabel(game.playedAt)}
          </>
        }
        badge={
          winner ? (
            <Badge variant="secondary">Équipe {winner} gagne</Badge>
          ) : (
            <Badge variant="outline">En attente</Badge>
          )
        }
      >
      <div className="grid grid-cols-2 divide-x">
        <TeamColumn
          team={aOnBlue ? "A" : "B"}
          side="blue"
          players={aOnBlue ? game.teamA : game.teamB}
          won={winner === (aOnBlue ? "A" : "B")}
          dofd={game.dofd}
        />
        <TeamColumn
          team={aOnBlue ? "B" : "A"}
          side="red"
          players={aOnBlue ? game.teamB : game.teamA}
          won={winner === (aOnBlue ? "B" : "A")}
          dofd={game.dofd}
        />
      </div>
      </GameLinkCard>
    </li>
  );
}

function TeamColumn({
  team,
  side,
  players,
  won,
  dofd,
}: {
  team: string;
  side: "blue" | "red";
  players: Schemas["GameHistoryParticipantDto"][];
  won: boolean;
  dofd: number[];
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
          <span className="flex min-w-0 items-center gap-1.5">
            <AvatarJoueur prenom={p.prenom} avatarUrl={p.avatarUrl} className="size-5 text-[9px]" />
            <span className="truncate">{p.prenom}</span>
            {dofd.includes(p.playerId) && <Badge variant="outline">DOFD</Badge>}
          </span>
          <RankBadge tier={p.rankTier} division={p.rankDivision} lp={null} className="text-[10px]" />
        </div>
      ))}
    </div>
  );
}
