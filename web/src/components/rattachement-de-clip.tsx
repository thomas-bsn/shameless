import { useQueryClient } from "@tanstack/react-query";
import { useId, useState, type FormEvent } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { dateHeureLabel } from "@/lib/dates";

type Candidate = Schemas["GameCandidateDuClip"];

/** Le libellé d'une Game proposée : « Game 2 · BO3 · ven. 16 oct., 22:40 ». */
export const candidateLabel = (game: Candidate) =>
  [game.seriesId == null ? `Game #${game.gameId}` : `Game ${game.gameNumber}`, game.bestOf != null && `BO${game.bestOf}`, dateHeureLabel(game.lanceeLe)]
    .filter(Boolean)
    .join(" · ");

/**
 * Après un changement de rattachement : la liste des clips sans Game et les clips de chaque Game se
 * relisent, pour que la page d'une Game reflète aussitôt l'association courante.
 */
function useRelireLesClips() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ["get", "/clips"] }),
      queryClient.invalidateQueries({ queryKey: ["get", "/clips/sans-game"] }),
      queryClient.invalidateQueries({ queryKey: ["get", "/games/{gameId}/clips"] }),
    ]);
}

/**
 * Rattache un clip à la main (ADR 0014) : une des Games lancées la veille ou le jour de sa publication,
 * ou une Game donnée par son numéro. Sert à rattacher un clip sans Game comme à corriger une Game fausse.
 */
export function RattacherClip({
  clip,
  sauf,
  onRattache,
}: {
  clip: Schemas["ClipResponse"];
  sauf?: number;
  /** Appelé une fois le clip rattaché et les listes relues. */
  onRattache?: () => void;
}) {
  const relire = useRelireLesClips();
  const candidates = $api.useQuery("get", "/clips/{id}/games-candidates", { params: { path: { id: clip.id } } });
  const rattacher = $api.useMutation("put", "/clips/{id}/game", {
    onSuccess: async () => {
      await relire();
      onRattache?.();
    },
  });
  const [numero, setNumero] = useState("");
  const champ = useId();

  const rattacherA = (gameId: number) => rattacher.mutate({ params: { path: { id: clip.id } }, body: { gameId } });
  const onSubmit = (event: FormEvent) => {
    event.preventDefault();
    const gameId = Number(numero);
    if (Number.isInteger(gameId) && gameId > 0) rattacherA(gameId);
  };
  const proposees = candidates.data?.filter((g) => g.gameId !== sauf);

  return (
    <div className="flex flex-col gap-3">
      {candidates.isPending ? (
        <Skeleton className="h-8 w-full" />
      ) : candidates.isError ? (
        <ErrorAlert error={candidates.error} />
      ) : proposees?.length === 0 ? (
        <p className="text-xs text-muted-foreground">Aucune Game lancée la veille ou le jour de ce clip.</p>
      ) : (
        <div className="flex flex-wrap gap-2" role="group" aria-label="Games proposées">
          {proposees?.map((game) => (
            <Button
              key={game.gameId}
              size="sm"
              variant="outline"
              disabled={rattacher.isPending}
              onClick={() => rattacherA(game.gameId)}
            >
              {candidateLabel(game)}
            </Button>
          ))}
        </div>
      )}
      <form onSubmit={onSubmit} className="flex items-end gap-2">
        <div className="flex flex-col gap-1">
          <Label htmlFor={champ} className="text-xs">
            N° de Game
          </Label>
          <Input
            id={champ}
            inputMode="numeric"
            value={numero}
            onChange={(event) => setNumero(event.target.value)}
            className="h-8 w-28"
          />
        </div>
        <Button type="submit" size="sm" disabled={rattacher.isPending || numero.trim() === ""}>
          Rattacher
        </Button>
      </form>
      {rattacher.isError && <ErrorAlert error={rattacher.error} />}
    </div>
  );
}

/** Retire le clip de sa Game : il revient parmi les clips à rattacher. */
export function RetirerClip({ clip, onRetire }: { clip: Schemas["ClipResponse"]; onRetire?: () => void }) {
  const relire = useRelireLesClips();
  const retirer = $api.useMutation("delete", "/clips/{id}/game", {
    onSuccess: async () => {
      await relire();
      onRetire?.();
    },
  });

  return (
    <>
      <Button
        size="sm"
        variant="ghost"
        disabled={retirer.isPending}
        onClick={() => retirer.mutate({ params: { path: { id: clip.id } } })}
      >
        Retirer de la Game
      </Button>
      {retirer.isError && <ErrorAlert error={retirer.error} />}
    </>
  );
}
