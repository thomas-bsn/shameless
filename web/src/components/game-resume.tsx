import type { Schemas } from "@/api/client";
import { EquipeSansStats, ScoreboardEquipe, sidesOf } from "@/components/game-scoreboard";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { gameWinner } from "@/lib/history";

type Recap = Schemas["GameRecapResponse"];

export function ResumeScoreboard({ game, compact = false }: { game: Recap; compact?: boolean }) {
  return (
    <>
      {sidesOf(game).map((s) => (
        <ScoreboardEquipe key={s.side} game={game} team={s.team} side={s.side} players={s.players} compact={compact} />
      ))}
    </>
  );
}

/** Une Game saisie à la main n'a ni stats ni note : on garde les équipes et le vainqueur, et on dit pourquoi. */
export function ResumeSansStats({ game }: { game: Recap }) {
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
