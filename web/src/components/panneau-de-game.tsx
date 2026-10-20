import { $api } from "@/api/client";
import { ResumeSansStats, ResumeScoreboard } from "@/components/game-resume";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { estSaisieAMain } from "@/lib/game";
import { Link } from "@tanstack/react-router";

/**
 * Le panneau déplié sous une ligne de game : le récapitulatif des deux équipes en densité compacte ; le détail est sur la page de la game.
 * Monté seulement à l'ouverture, donc le détail n'est chargé qu'à ce moment.
 */
export function PanneauDeGame({ gameId }: { gameId: number }) {
  const game = $api.useQuery("get", "/games/{id}", { params: { path: { id: gameId } } });
  return (
    <div className="flex flex-col gap-3 rounded-b-xl border-t border-white/10 bg-black/25 p-3">
      {game.isPending ? (
        <LoadingRows />
      ) : game.isError ? (
        <ErrorAlert error={game.error} />
      ) : (
        <>
          {estSaisieAMain(game.data) ? (
            <ResumeSansStats game={game.data} />
          ) : (
            <ResumeScoreboard game={game.data} compact />
          )}
        </>
      )}
      <Link
        to="/games/$gameId"
        params={{ gameId: String(gameId) }}
        className="self-center text-sm text-muted-foreground hover:text-foreground"
      >
        Voir la game complète ›
      </Link>
    </div>
  );
}
