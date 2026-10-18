import { useState } from "react";
import { createFileRoute, Link } from "@tanstack/react-router";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { RankBadge } from "@/components/rank";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { gameWinner, type Month, monthLabel, seriesScore, shiftMonth } from "@/lib/history";
import { outcomeLabel } from "@/lib/series";
import { sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";
import { dateHeureLabel } from "@/lib/dates";

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
  const [selectedDay, setSelectedDay] = useState<string | null>(null);
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
  const playedByDay = new Map<string, number>();
  for (const s of series) for (const g of s.games) { const key = new Date(g.playedAt).toLocaleDateString("sv-SE"); playedByDay.set(key, (playedByDay.get(key) ?? 0) + 1); }
  const monthKey = `${shown.year}-${String(shown.month).padStart(2, "0")}`;
  const selection = selectedDay?.startsWith(`${monthKey}-`) ? selectedDay : null;
  const daysInMonth = new Date(shown.year, shown.month, 0).getDate();
  const firstOffset = (new Date(shown.year, shown.month - 1, 1).getDay() + 6) % 7;

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
          <section aria-label={`Calendrier ${monthLabel(shown)}`} className="rounded-xl border border-border/70 bg-card/60 p-4 backdrop-blur"><div className="grid grid-cols-7 gap-1 text-center text-xs text-muted-foreground">{["Lun", "Mar", "Mer", "Jeu", "Ven", "Sam", "Dim"].map((day) => <span key={day} className="py-2">{day}</span>)}{Array.from({ length: firstOffset }, (_, i) => <span key={`empty-${i}`} />)}{Array.from({ length: daysInMonth }, (_, i) => { const date = `${monthKey}-${String(i + 1).padStart(2, "0")}`; const count = playedByDay.get(date) ?? 0; return <Button key={date} type="button" variant={selection === date ? "default" : count ? "secondary" : "ghost"} aria-pressed={selection === date} disabled={!count} className="h-12 flex-col gap-0 p-1" onClick={() => setSelectedDay(selection === date ? null : date)}><span>{i + 1}</span>{count > 0 && <span className="text-[9px] opacity-75">{count} game{count === 1 ? "" : "s"}</span>}</Button>; })}</div><p className="mt-2 text-center text-xs text-muted-foreground">{selection ? `Games du ${new Date(`${selection}T12:00:00`).toLocaleDateString("fr-FR", { day: "numeric", month: "long" })}` : "Sélectionne un jour pour filtrer les soirées."}</p></section>
          <p className="text-sm text-muted-foreground">
            {series.length} série{series.length > 1 ? "s" : ""} · {gameCount} game{gameCount > 1 ? "s" : ""}
          </p>
          {series.map((s) => {
            const games = selection ? s.games.filter((g) => new Date(g.playedAt).toLocaleDateString("sv-SE") === selection) : s.games;
            return games.length > 0 && <SeriesCard key={s.seriesId} series={s} games={games} />;
          })}
        </>
      )}
    </div>
  );
}

function SeriesCard({ series, games = series.games }: { series: Schemas["SeriesHistoryResponse"]; games?: Schemas["GameHistoryResponse"][] }) {
  // seriesId 0 : games enregistrées sans Série.
  const title = series.seriesId === 0 ? "Games hors série" : `Série BO${series.format}`;
  const score = seriesScore(games);

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
            {games.map((g, index) => (
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
          <Link to="/games/$gameId" params={{ gameId: String(game.id) }} className="hover:text-foreground">
            Game {index}
          </Link>{" "}
          · #{game.id} · {dateHeureLabel(game.playedAt)}
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
          mvp={game.mvp}
          dofd={game.dofd}
        />
        <TeamColumn
          team={aOnBlue ? "B" : "A"}
          side="red"
          players={aOnBlue ? game.teamB : game.teamA}
          won={winner === (aOnBlue ? "B" : "A")}
          mvp={game.mvp}
          dofd={game.dofd}
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
  mvp,
  dofd,
}: {
  team: string;
  side: "blue" | "red";
  players: Schemas["GameHistoryParticipantDto"][];
  won: boolean;
  mvp: number[];
  dofd: number[];
}) {
  const label = `Équipe ${team} · ${side === "blue" ? "Blue" : "Red"} side`;
  const cost = players.reduce((sum, p) => sum + (p.cost ?? 0), 0);

  return (
    <div role="group" aria-label={label} className={cn("flex flex-col gap-2 p-3", won && "bg-emerald-600/5 dark:bg-emerald-400/5")}>
      <div className="flex items-center justify-between text-xs font-semibold">
        <span className={sideTextClass(side)}>{label}</span>
        <span className="text-muted-foreground">{cost} pts</span>
      </div>
      {players.map((p) => (
        <div key={p.riotId} className="flex items-center justify-between gap-2 text-sm">
          <span className="flex min-w-0 items-center gap-1.5">
            <span className="truncate">{p.prenom}</span>
            {mvp.includes(p.playerId) && <Badge variant="secondary">MVP</Badge>}
            {dofd.includes(p.playerId) && <Badge variant="outline">DOFD</Badge>}
          </span>
          <RankBadge tier={p.rankTier} division={p.rankDivision} lp={null} className="text-[10px]" />
        </div>
      ))}
    </div>
  );
}
