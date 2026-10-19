import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useState, type FormEvent } from "react";
import { $api, type Schemas } from "@/api/client";
import { DraftChampionsOrganisateur } from "@/components/draft-champions-organisateur";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { RapportEnRevuePanel } from "@/components/rapport-en-revue-panel";
import { SeriesOptionsForm } from "@/components/series-options-form";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { pendingGame,outcomeLabel, replacementCandidates, sideLabel } from "@/lib/series";
import { sideTextClass } from "@/lib/tier-styles";

export const Route = createFileRoute("/_connecte/series/$seriesId_/organisateur")({
  component: EspaceOrganisateurPage,
});

type Series = Schemas["SeriesResponse"];

/** Après une action, relit la Série et la liste des séries de la Soirée. */
function useRefreshSeries() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ["get", "/series/{id}"] }),
      queryClient.invalidateQueries({ queryKey: ["get", "/soirees/{id}/series"] }),
    ]);
}

function EspaceOrganisateurPage() {
  const { seriesId } = Route.useParams();
  const id = Number(seriesId);
  const series = $api.useQuery("get", "/series/{id}", { params: { path: { id } } });

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <Link
        to="/series/$seriesId"
        params={{ seriesId }}
        className="text-sm text-muted-foreground hover:text-foreground"
      >
        ← Retour au récap
      </Link>
      {series.isPending ? (
        <LoadingRows />
      ) : series.isError ? (
        <ErrorAlert error={series.error} />
      ) : (
        <EspaceOrganisateur series={series.data} />
      )}
    </div>
  );
}

/**
 * L'Espace organisateur (ADR 0017) : le pilotage d'une Série, réservé à l'organisateur connecté. Tout
 * ce qui est commande vit ici, la Page de la Série restant un récap.
 */
function EspaceOrganisateur({ series }: { series: Series }) {
  const enCours = series.outcome === "EnCours";
  const refresh = useRefreshSeries();
  const pending = pendingGame(series);

  return (
    <>
      <div>
        <h1 className="flex items-center gap-2 text-2xl font-bold">
          Pilotage · Série BO{series.format}
          {series.fearless && <Badge variant="outline">Fearless</Badge>}
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">{outcomeLabel(series)}</p>
      </div>

      {enCours ? (
        <>
          <GamePanel series={series} />
          {pending?.rapportEnRevue && (
            <RapportEnRevuePanel
              key={pending.rapportEnRevue.id}
              revue={pending.rapportEnRevue}
              numero={series.games.length}
              onDecided={refresh}
            />
          )}
          {pending && <DraftChampionsOrganisateur key={pending.id} series={series} gameId={pending.id} />}
          <ReplacementPanel series={series} />
          <div className="flex flex-wrap items-start gap-3">
            <Button asChild variant="outline">
              <Link to="/series/$seriesId/preparation" params={{ seriesId: String(series.seriesId) }}>
                Liens et code
              </Link>
            </Button>
            <StopPanel series={series} />
          </div>
        </>
      ) : (
        <RevanchePanel series={series} />
      )}
    </>
  );
}

/** Saisit le résultat de la game en cours ; la game suivante s'ouvre quand les deux Capitaines sont prêts (Side automatique : pile ou face, puis alternance). */
function GamePanel({ series }: { series: Series }) {
  const refresh = useRefreshSeries();
  const pending = pendingGame(series);
  const number = series.games.length + (pending ? 0 : 1);
  const result = $api.useMutation("put", "/games/{id}/result", { onSuccess: refresh });

  return (
    <section aria-labelledby="game-en-cours">
      <Card>
        <CardHeader>
          <CardTitle id="game-en-cours">{pending ? `Game ${number} en cours` : `Game ${number}`}</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {pending ? (
            <>
              <p className="text-sm">
                Équipe A <span className={sideTextClass(pending.teamASide)}>{sideLabel(pending.teamASide)}</span> · Équipe B{" "}
                <span className={sideTextClass(pending.teamBSide)}>{sideLabel(pending.teamBSide)}</span>
              </p>
              {pending.startedAt ? (
                <p className="text-sm text-muted-foreground">
                  Partie commencée à{" "}
                  {new Date(pending.startedAt).toLocaleTimeString("fr-FR", { hour: "2-digit", minute: "2-digit" })}.
                </p>
              ) : (
                <p className="text-sm text-muted-foreground">
                  La draft de champions est en cours : la partie commence à son dernier pick.
                </p>
              )}
              {pending.rapportEnRevue && (
                <p className="text-sm text-muted-foreground">
                  Un rapport du Companion est en revue : accepte-le ou rejette-le avant de saisir le résultat.
                </p>
              )}
              <p className="text-xs text-muted-foreground">
                Saisie manuelle de secours, sans Companion : le résultat de la game.
              </p>
              <div className="flex flex-wrap gap-2">
                {(["A", "B"] as const).map((winner) => (
                  <Button
                    key={winner}
                    disabled={result.isPending || pending.rapportEnRevue !== null}
                    onClick={() => result.mutate({ params: { path: { id: pending.id } }, body: { winner } })}
                  >
                    Victoire Équipe {winner}
                  </Button>
                ))}
              </div>
              {result.isError && <ErrorAlert error={result.error} />}
            </>
          ) : (
            <p className="text-sm text-muted-foreground">
              La game {number} s'ouvre toute seule quand les deux Capitaines sont prêts : chacun se déclare prêt depuis
              son Lien capitaine, game après game. Rien à lancer ici.
            </p>
          )}
        </CardContent>
      </Card>
    </section>
  );
}

/** Remplace un joueur d'une Équipe par un joueur hors de la Série, pour les games suivantes. */
function ReplacementPanel({ series }: { series: Series }) {
  const refresh = useRefreshSeries();
  const players = $api.useQuery("get", "/players");
  const [replaced, setReplaced] = useState("");
  const [replacement, setReplacement] = useState("");
  const replace = $api.useMutation("post", "/series/{id}/replacements", {
    onSuccess: async () => {
      setReplaced("");
      setReplacement("");
      await refresh();
    },
  });
  const candidates = replacementCandidates(players.data ?? [], series);

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    replace.mutate({
      params: { path: { id: series.seriesId } },
      body: { replacedPlayerId: Number(replaced), replacementPlayerId: Number(replacement) },
    });
  }

  return (
    <section aria-labelledby="remplacement">
      <Card>
        <CardHeader>
          <CardTitle id="remplacement">Remplacement</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={onSubmit} className="flex flex-wrap items-end gap-3">
            <div className="flex min-w-48 flex-col gap-2">
              <Label htmlFor="joueur-remplace">Joueur qui sort</Label>
              <Select value={replaced} onValueChange={setReplaced}>
                <SelectTrigger id="joueur-remplace" className="w-full">
                  <SelectValue placeholder="Choisir…" />
                </SelectTrigger>
                <SelectContent>
                  {(["A", "B"] as const).map((equipe) => (
                    <SelectGroup key={equipe}>
                      <SelectLabel>Équipe {equipe}</SelectLabel>
                      {(equipe === "A" ? series.teamA : series.teamB).map((p) => (
                        <SelectItem key={p.playerId} value={String(p.playerId)}>
                          {p.prenom}
                        </SelectItem>
                      ))}
                    </SelectGroup>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <div className="flex min-w-48 flex-col gap-2">
              <Label htmlFor="joueur-remplacant">Joueur qui entre</Label>
              <Select value={replacement} onValueChange={setReplacement}>
                <SelectTrigger id="joueur-remplacant" className="w-full">
                  <SelectValue placeholder="Choisir…" />
                </SelectTrigger>
                <SelectContent>
                  {candidates.map((p) => (
                    <SelectItem key={p.id} value={String(p.id)}>
                      {p.prenom}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </div>
            <Button type="submit" variant="outline" disabled={replace.isPending || !replaced || !replacement}>
              Remplacer
            </Button>
            {(replace.isError || players.isError) && (
              <div className="w-full">
                <ErrorAlert error={replace.error ?? players.error} />
              </div>
            )}
          </form>
        </CardContent>
      </Card>
    </section>
  );
}

/** Arrêter : l'API annule la Série sans game terminée, sinon l'interrompt (ADR 0009). */
function StopPanel({ series }: { series: Series }) {
  const refresh = useRefreshSeries();
  const navigate = useNavigate();
  const stop = $api.useMutation("post", "/series/{id}/stop", {
    onSuccess: async (stopped) => {
      await refresh();
      if (stopped.arret === "Annulee") await navigate({ to: "/soiree" });
    },
  });
  const hasCompletedGame = series.games.some((g) => g.winner);
  const consequence = hasCompletedGame
    ? "La série sera interrompue : les games terminées sont gardées, la game en attente est supprimée."
    : "Aucune game n'est terminée : la série sera annulée et effacée.";

  return (
    <section aria-label="Arrêter la série" className="flex flex-col gap-2">
      <Button
        variant="destructive"
        className="self-start"
        disabled={stop.isPending}
        onClick={() => {
          if (window.confirm(`Arrêter la série ?\n${consequence}`)) stop.mutate({ params: { path: { id: series.seriesId } } });
        }}
      >
        Arrêter la série
      </Button>
      {stop.isError && <ErrorAlert error={stop.error} />}
    </section>
  );
}

/**
 * Une Série terminée : Revanche entre les mêmes Équipes dans leur dernière composition, ou de
 * nouvelles Équipes formées au Picker.
 */
function RevanchePanel({ series }: { series: Series }) {
  const navigate = useNavigate();
  const refresh = useRefreshSeries();
  const rematch = $api.useMutation("post", "/series/{id}/rematch", {
    onSuccess: async (created) => {
      await refresh();
      // La nouvelle Série s'ouvre sur son Écran de préparation (Capitaines, liens), pas sur le récap.
      await navigate({ to: "/series/$seriesId/preparation", params: { seriesId: String(created.seriesId) } });
    },
  });

  return (
    <section aria-labelledby="revanche">
      <Card>
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2">
          <CardTitle id="revanche">Revanche</CardTitle>
          <Button asChild variant="outline">
            <Link to="/soiree/picker">Former de nouvelles équipes</Link>
          </Button>
        </CardHeader>
        <CardContent>
          <SeriesOptionsForm
            initial={{ format: series.format, fearless: series.fearless }}
            submitLabel="Lancer la revanche"
            pending={rematch.isPending}
            error={rematch.isError ? rematch.error : null}
            onSubmit={(options) => rematch.mutate({ params: { path: { id: series.seriesId } }, body: options })}
          />
        </CardContent>
      </Card>
    </section>
  );
}
