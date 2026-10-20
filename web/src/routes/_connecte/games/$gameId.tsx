import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { $api, type Schemas } from "@/api/client";
import { EquipeSansStats, ScoreboardEquipe, sidesOf } from "@/components/game-scoreboard";
import { OngletReplay, OngletStats } from "@/components/game-stats-onglets";
import { BoutonModifierLaGame } from "@/components/modifier-game/bouton-modifier-la-game";
import { GameClips } from "@/components/game-clips";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent } from "@/components/ui/card";
import { PronosticsDeLaGame } from "@/components/pronostics-de-la-game";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { VotesDeLaGame } from "@/components/votes-de-la-game";
import { dureeLabel, estSaisieAMain } from "@/lib/game";
import { gameWinner } from "@/lib/history";
import { dateHeureLabel, dateLabel } from "@/lib/dates";

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

  return (
    <>
      <Banniere game={game} />
      <Tabs defaultValue="resume">
        <TabsList>
          <TabsTrigger value="resume">Résumé</TabsTrigger>
          <TabsTrigger value="degats">Dégâts</TabsTrigger>
          <TabsTrigger value="objectifs">Objectifs</TabsTrigger>
          <TabsTrigger value="vision">Vision</TabsTrigger>
          <TabsTrigger value="replay" className="opacity-60">
            Replay
          </TabsTrigger>
        </TabsList>
        <TabsContent value="resume" className="flex flex-col gap-4">
          {saisieAMain ? <ResumeSansStats game={game} /> : <ResumeScoreboard game={game} />}
        </TabsContent>
        {(["degats", "objectifs", "vision"] as const).map((onglet) => (
          <TabsContent key={onglet} value={onglet} className="flex flex-col gap-4">
            {saisieAMain ? <ResumeSansStats game={game} /> : <OngletStats game={game} onglet={onglet} />}
          </TabsContent>
        ))}
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

function Banniere({ game }: { game: Recap }) {
  const navigate = useNavigate();
  const winner = gameWinner(game);
  const meta =
    game.seriesId == null
      ? `Game #${game.id}`
      : `Game ${game.gameNumber} · Série BO${game.bestOf}`;
  const duree = game.match?.durationSeconds != null ? dureeLabel(game.match.durationSeconds) : null;
  const titre = winner ? [`Victoire de l'Équipe ${winner}`, duree].filter(Boolean).join(" · ") : meta;

  return (
    <Card>
      <CardContent className="flex flex-wrap items-center justify-between gap-4">
        <div>
          <p className="text-sm text-muted-foreground">
            {winner ? `${meta} · ` : ""}
            {dateHeureLabel(game.playedAt)}
          </p>
          <h1 className="text-2xl font-bold">{titre}</h1>
          {game.modifieeParAdminLe && (
            <Badge variant="secondary" className="mt-2">
              modifiée par un admin le {dateLabel(game.modifieeParAdminLe)}
            </Badge>
          )}
        </div>
        <div className="flex flex-wrap items-center gap-3">
          {game.seriesId != null && (
            <span className="font-mono font-semibold tabular-nums">
              Équipe A {game.teamAWins} – {game.teamBWins} Équipe B
            </span>
          )}
          {!winner && <Badge variant="outline">En cours</Badge>}
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
      </CardContent>
    </Card>
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
