import { useQueryClient } from "@tanstack/react-query";
import { AlertDialog } from "radix-ui";
import { useState } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { dateHeureLabel } from "@/lib/dates";

const equipes = ["A", "B"] as const;

/** Recharge ce que le bandeau affiche une fois une action passée. */
function useRafraichirLeBandeau() {
  const queryClient = useQueryClient();
  return async () => {
    await Promise.all([
      queryClient.invalidateQueries({ queryKey: ["get", "/games/pending"] }),
      queryClient.invalidateQueries({ queryKey: ["get", "/series/active"] }),
    ]);
  };
}

function GameSansResultat({ game }: { game: Schemas["PendingGameResponse"] }) {
  const rafraichir = useRafraichirLeBandeau();
  const resultat = $api.useMutation("put", "/games/{id}/result", {
    onSuccess: rafraichir,
  });
  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        <div>
          <p className="font-medium">
            {game.seriesId !== null && `Série ${game.seriesId} · `}Game{" "}
            {game.id} : qui a gagné ?
          </p>
          <p className="text-sm text-muted-foreground">
            Lancée le {dateHeureLabel(game.playedAt)}, aucun résultat reçu
          </p>
          {equipes.map((equipe) => {
            const noms = game.participants
              .filter((p) => p.equipe === equipe)
              .map((p) => p.prenom);
            return (
              noms.length > 0 && (
                <p key={equipe} className="text-xs text-muted-foreground">
                  Équipe {equipe} : {noms.join(", ")}
                </p>
              )
            );
          })}
        </div>
        <div className="flex flex-wrap gap-2">
          {equipes.map((winner) => (
            <Button
              key={winner}
              disabled={resultat.isPending}
              onClick={() =>
                resultat.mutate({
                  params: { path: { id: game.id } },
                  body: { winner },
                })
              }
            >
              Victoire Équipe {winner}
            </Button>
          ))}
        </div>
        {resultat.isError && <ErrorAlert error={resultat.error} />}
      </CardContent>
    </Card>
  );
}

function SerieEnCours({ series }: { series: Schemas["SeriesResponse"] }) {
  const rafraichir = useRafraichirLeBandeau();
  const [ouvert, setOuvert] = useState(false);
  const arret = $api.useMutation("post", "/series/{id}/stop", {
    onSuccess: async () => {
      setOuvert(false);
      await rafraichir();
    },
  });
  return (
    <Card>
      <CardContent className="flex flex-col gap-3">
        <div>
          <p className="font-medium">Série {series.seriesId} en cours</p>
          <p className="text-sm text-muted-foreground">
            Équipe A{" "}
            <span className="font-mono">
              {series.teamAWins} — {series.teamBWins}
            </span>{" "}
            Équipe B · BO{series.format}
          </p>
        </div>
        <AlertDialog.Root open={ouvert} onOpenChange={setOuvert}>
          <AlertDialog.Trigger asChild>
            <Button variant="destructive" className="self-start">
              Interrompre la série
            </Button>
          </AlertDialog.Trigger>
          <AlertDialog.Portal>
            <AlertDialog.Overlay className="fixed inset-0 z-50 bg-black/50" />
            <AlertDialog.Content className="fixed top-1/2 left-1/2 z-50 flex w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 flex-col gap-4 rounded-xl border bg-card p-6 text-card-foreground shadow-lg">
              <AlertDialog.Title className="text-lg font-semibold">
                Interrompre la série {series.seriesId} ?
              </AlertDialog.Title>
              <AlertDialog.Description className="text-sm text-muted-foreground">
                Les games jouées restent enregistrées et la game en attente est
                supprimée. Sans aucune game jouée, la série est annulée et
                effacée.
              </AlertDialog.Description>
              {arret.isError && <ErrorAlert error={arret.error} />}
              <div className="flex justify-end gap-2">
                <AlertDialog.Cancel asChild>
                  <Button variant="outline">Annuler</Button>
                </AlertDialog.Cancel>
                <Button
                  variant="destructive"
                  disabled={arret.isPending}
                  onClick={() =>
                    arret.mutate({ params: { path: { id: series.seriesId } } })
                  }
                >
                  Interrompre
                </Button>
              </div>
            </AlertDialog.Content>
          </AlertDialog.Portal>
        </AlertDialog.Root>
      </CardContent>
    </Card>
  );
}

/**
 * Le bandeau « À traiter » : games sans résultat et séries en cours.
 * Quand il n'y a rien, il laisse place à une ligne calme.
 */
export function ATraiter() {
  const games = $api.useQuery("get", "/games/pending");
  const actives = $api.useQuery("get", "/series/active");

  if (games.isError || actives.isError)
    return <ErrorAlert error={games.error ?? actives.error} />;
  if (games.isPending || actives.isPending) return null;

  const series = actives.data.series;
  const total = games.data.length + series.length;
  if (total === 0) {
    return (
      <p className="mb-6 rounded-md border bg-card p-3 text-sm">
        <strong>Rien à traiter</strong> : aucune game sans résultat, aucune
        série en cours.
      </p>
    );
  }
  return (
    <section
      aria-label="À traiter"
      className="mb-6 flex flex-col gap-3 rounded-xl border border-amber-500/40 bg-amber-500/5 p-4"
    >
      <h2 className="text-xs font-semibold tracking-wide text-amber-500 uppercase">
        À traiter · {total}
      </h2>
      <div className="grid gap-3 md:grid-cols-2">
        {games.data.map((game) => (
          <GameSansResultat key={`game-${game.id}`} game={game} />
        ))}
        {series.map((s) => (
          <SerieEnCours key={`serie-${s.seriesId}`} series={s} />
        ))}
      </div>
    </section>
  );
}
