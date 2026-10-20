import { useState } from "react";
import { createFileRoute, Link } from "@tanstack/react-router";
import { BarreDOngletsGame } from "@/components/barre-d-onglets-game";
import { VueEnCibles } from "@/components/game-cibles";
import { useVueDeGame } from "@/lib/vue-de-game";
import { $api, type Schemas } from "@/api/client";
import { OngletRunes } from "@/components/game-runes";
import { EquipeSansStats, ScoreboardEquipe, sidesOf } from "@/components/game-scoreboard";
import { OngletDetails, OngletReplay } from "@/components/game-stats-onglets";
import { EnteteDeGame } from "@/components/game-entete";
import { GameClips } from "@/components/game-clips";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { PronosticsDeLaGame } from "@/components/pronostics-de-la-game";
import { Tabs, TabsContent } from "@/components/ui/tabs";
import { VotesDeLaGame } from "@/components/votes-de-la-game";
import { estSaisieAMain } from "@/lib/game";
import { gameWinner } from "@/lib/history";

/** La page d'une Game : c'est le lien de récap posté par le bot Discord. */
export const Route = createFileRoute("/_connecte/games/$gameId")({
  component: GamePage,
});

type Recap = Schemas["GameRecapResponse"];

function GamePage() {
  const { gameId } = Route.useParams();
  const game = $api.useQuery("get", "/games/{id}", { params: { path: { id: Number(gameId) } } });

  return (
    <div className="flex max-w-6xl flex-col gap-6">
      <Link to="/historique" className="text-sm text-muted-foreground hover:text-foreground">
        ← Historique
      </Link>
      {game.isPending ? <LoadingRows /> : game.isError ? <ErrorAlert error={game.error} /> : <Recit game={game.data} />}
    </div>
  );
}

function Recit({ game }: { game: Recap }) {
  const saisieAMain = estSaisieAMain(game);
  const [vue, choisirVue] = useVueDeGame();
  // Général (liste) et Cibles sont deux vues du même résumé : on retient la dernière visitée.
  const [onglet, setOnglet] = useState<string>(vue === "cibles" ? "cibles" : "general");
  const changerOnglet = (valeur: string) => {
    setOnglet(valeur);
    if (valeur === "general") choisirVue("liste");
    if (valeur === "cibles") choisirVue("cibles");
  };

  return (
    <>
      <EnteteDeGame game={game} />
      <Tabs value={onglet} onValueChange={changerOnglet}>
        <BarreDOngletsGame />
        <TabsContent value="general" className="flex flex-col gap-4">
          {saisieAMain ? <ResumeSansStats game={game} /> : <ResumeScoreboard game={game} />}
        </TabsContent>
        <TabsContent value="cibles" className="flex flex-col gap-4">
          <VueEnCibles game={game} />
        </TabsContent>
        <TabsContent value="details" className="flex flex-col gap-4">
          {saisieAMain ? <ResumeSansStats game={game} /> : <OngletDetails game={game} />}
        </TabsContent>
        <TabsContent value="runes" className="flex flex-col gap-4">
          {saisieAMain ? <ResumeSansStats game={game} /> : <OngletRunes game={game} />}
        </TabsContent>
        <TabsContent value="replay">
          <OngletReplay />
        </TabsContent>
      </Tabs>
      <div className="grid items-start gap-6 lg:grid-cols-3">
        <VotesDeLaGame gameId={game.id} />
        <PronosticsDeLaGame gameId={game.id} />
        <GameClips gameId={game.id} />
      </div>
    </>
  );
}

function ResumeScoreboard({ game }: { game: Recap }) {
  return (
    <>
      {sidesOf(game).map((s) => (
        <ScoreboardEquipe key={s.side} game={game} team={s.team} side={s.side} players={s.players} />
      ))}
    </>
  );
}

/** Une Game saisie à la main n'a ni stats ni note : on garde les équipes et le vainqueur, et on dit pourquoi. */
function ResumeSansStats({ game }: { game: Recap }) {
  const winner = gameWinner(game);
  return (
    <>
      <Alert>
        <AlertDescription>
          <strong>Résultat saisi à la main.</strong> Les stats détaillées (K/D/A, CS, or, dégâts) ne sont pas disponibles
          pour cette game, et les joueurs n'ont pas de note.
        </AlertDescription>
      </Alert>
      <div className="grid gap-4 md:grid-cols-2">
        {sidesOf(game).map((s) => (
          <EquipeSansStats key={s.side} team={s.team} side={s.side} players={s.players} won={winner === s.team} />
        ))}
      </div>
    </>
  );
}
