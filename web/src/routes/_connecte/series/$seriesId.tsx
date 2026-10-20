import { createFileRoute, Link } from "@tanstack/react-router";
import { $api, type Schemas } from "@/api/client";
import { ChampionsJoues } from "@/components/champions-joues";
import { DraftChampionsTerminee } from "@/components/draft-champions-organisateur";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { TeamCard } from "@/components/team-card";
import { Badge } from "@/components/ui/badge";
import { GameLinkCard } from "@/components/game-link-card";
import { Button } from "@/components/ui/button";
import { outcomeLabel, sideLabel } from "@/lib/series";
import { sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/_connecte/series/$seriesId")({
  component: SeriePage,
});

type Series = Schemas["SeriesResponse"];

function SeriePage() {
  const { seriesId } = Route.useParams();
  const id = Number(seriesId);
  const series = $api.useQuery("get", "/series/{id}", { params: { path: { id } } });

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <Link to="/soiree" className="text-sm text-muted-foreground hover:text-foreground">
        ← Soirée
      </Link>
      {series.isPending ? (
        <LoadingRows />
      ) : series.isError ? (
        <ErrorAlert error={series.error} />
      ) : (
        <SerieView series={series.data} />
      )}
    </div>
  );
}

/**
 * La Page de la Série (ADR 0017) : un récap à lien stable, sans secret ni commande. Les commandes de
 * l'organisateur vivent dans l'Espace organisateur, dont le bouton n'est offert qu'à une session connectée
 * (toute la page l'est).
 */
function SerieView({ series }: { series: Series }) {
  return (
    <>
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-bold">
            Série BO{series.format}
            {series.fearless && <Badge variant="outline">Fearless</Badge>}
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">{outcomeLabel(series)}</p>
        </div>
        <div className="flex flex-wrap items-center gap-4">
          <Button asChild variant="outline">
            <Link to="/series/$seriesId/organisateur" params={{ seriesId: String(series.seriesId) }}>
              Espace organisateur
            </Link>
          </Button>
          <p aria-label="Score" className="text-3xl font-bold tabular-nums">
            <span className={cn(series.winningTeam === "A" && "text-emerald-400")}>A {series.teamAWins}</span>
            <span className="mx-3 text-muted-foreground">–</span>
            <span className={cn(series.winningTeam === "B" && "text-emerald-400")}>{series.teamBWins} B</span>
          </p>
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <TeamCard equipe="A" players={series.teamA} />
        <TeamCard equipe="B" players={series.teamB} />
      </div>

      <GamesList series={series} />
    </>
  );
}

function GamesList({ series }: { series: Series }) {
  if (series.games.length === 0) return null;

  return (
    <section aria-labelledby="games-serie" className="flex flex-col gap-3">
      <h2 id="games-serie" className="text-lg font-semibold">
        Games
      </h2>
      <ol aria-label="Games de la série" className="flex flex-col gap-2">
        {series.games.map((g, index) => (
          <li key={g.id}>
            <GameLinkCard
              gameId={g.id}
              label={`Voir la game ${index + 1}`}
              header={
                <>
                  Game {index + 1} · Équipe A <span className={sideTextClass(g.teamASide)}>{sideLabel(g.teamASide)}</span> ·
                  Équipe B <span className={sideTextClass(g.teamBSide)}>{sideLabel(g.teamBSide)}</span>
                </>
              }
              badge={
                g.winner ? (
                  <Badge variant="secondary">Équipe {g.winner} gagne</Badge>
                ) : (
                  <Badge variant="outline">En attente</Badge>
                )
              }
            >
              <div className="flex flex-col gap-2 px-3 py-2">
                <ChampionsJoues game={g} />
                {g.status === "completed" && <DraftChampionsTerminee gameId={g.id} />}
              </div>
            </GameLinkCard>
          </li>
        ))}
      </ol>
    </section>
  );
}
