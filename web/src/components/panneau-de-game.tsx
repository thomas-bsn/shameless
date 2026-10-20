import { $api } from "@/api/client";
import { ResumeSansStats, ResumeScoreboard } from "@/components/game-resume";
import { OngletRunes } from "@/components/game-runes";
import { OngletDetails } from "@/components/game-stats-onglets";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { estSaisieAMain } from "@/lib/game";
import { Link } from "@tanstack/react-router";

/**
 * Le panneau déplié sous une ligne de game : mêmes onglets et mêmes données que la page de la game.
 * Monté seulement à l'ouverture, donc le détail n'est chargé qu'à ce moment.
 */
export function PanneauDeGame({ gameId }: { gameId: number }) {
  const game = $api.useQuery("get", "/games/{id}", { params: { path: { id: gameId } } });
  return (
    <div className="flex flex-col gap-3 rounded-b-xl border-t bg-card/60 p-3">
      {game.isPending ? (
        <LoadingRows />
      ) : game.isError ? (
        <ErrorAlert error={game.error} />
      ) : (
        <Tabs defaultValue="general">
          <TabsList variant="line">
            <TabsTrigger value="general">Général</TabsTrigger>
            <TabsTrigger value="details">Détails</TabsTrigger>
            <TabsTrigger value="runes">Runes</TabsTrigger>
          </TabsList>
          <TabsContent value="general" className="flex flex-col gap-3">
            {estSaisieAMain(game.data) ? <ResumeSansStats game={game.data} /> : <ResumeScoreboard game={game.data} />}
          </TabsContent>
          <TabsContent value="details" className="flex flex-col gap-3">
            {estSaisieAMain(game.data) ? <ResumeSansStats game={game.data} /> : <OngletDetails game={game.data} />}
          </TabsContent>
          <TabsContent value="runes" className="flex flex-col gap-3">
            {estSaisieAMain(game.data) ? <ResumeSansStats game={game.data} /> : <OngletRunes game={game.data} />}
          </TabsContent>
        </Tabs>
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
