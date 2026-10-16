import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useState, type FormEvent } from "react";
import { $api, type Schemas } from "@/api/client";
import { CapitainesLiensPanel } from "@/components/capitaines-liens-panel";
import { DraftChampionsOrganisateur, DraftChampionsTerminee } from "@/components/draft-champions-organisateur";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { SeriesOptionsForm } from "@/components/series-options-form";
import { TeamCard } from "@/components/team-card";
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
import { pendingGame, outcomeLabel, replacementCandidates } from "@/lib/series";
import { sideTextClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/_connecte/series/$seriesId")({
  component: SeriePage,
});

type Series = Schemas["SeriesResponse"];

const sideLabel = (side: string) => (side === "red" ? "Red side" : "Blue side");

/** Après une action, relit la Série et la liste des séries de la Soirée. */
function useRefreshSeries() {
  const queryClient = useQueryClient();
  return () =>
    Promise.all([
      queryClient.invalidateQueries({ queryKey: ["get", "/series/{id}"] }),
      queryClient.invalidateQueries({ queryKey: ["get", "/soirees/{id}/series"] }),
    ]);
}

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

function SerieView({ series }: { series: Series }) {
  const enCours = series.outcome === "EnCours";
  const refresh = useRefreshSeries();
  const pending = pendingGame(series);

  return (
    <>
      <div className="flex flex-wrap items-end justify-between gap-4">
        <div>
          <h1 className="flex items-center gap-2 text-2xl font-bold">
            Série BO{series.format}
            {series.fearless && <Badge variant="outline">Fearless</Badge>}
            {series.alternateSides && <Badge variant="outline">Sides alternés</Badge>}
          </h1>
          <p className="mt-1 text-sm text-muted-foreground">{outcomeLabel(series)}</p>
        </div>
        <p aria-label="Score" className="text-3xl font-bold tabular-nums">
          <span className={cn(series.winningTeam === "A" && "text-emerald-400")}>A {series.teamAWins}</span>
          <span className="mx-3 text-muted-foreground">–</span>
          <span className={cn(series.winningTeam === "B" && "text-emerald-400")}>{series.teamBWins} B</span>
        </p>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <TeamCard equipe="A" players={series.teamA} capitaine={series.capitaineA} />
        <TeamCard equipe="B" players={series.teamB} capitaine={series.capitaineB} />
      </div>

      {enCours ? (
        <>
          <CapitainesLiensPanel series={series} onSeriesChanged={refresh} />
          <GamePanel series={series} />
          {pending && <DraftChampionsOrganisateur key={pending.id} series={series} gameId={pending.id} />}
          <ReplacementPanel series={series} />
          <StopPanel series={series} />
        </>
      ) : (
        <RevanchePanel series={series} />
      )}

      <GamesList series={series} />
    </>
  );
}

/** Lance la prochaine game avec le Side de chaque Équipe, ou saisit le résultat de la game en cours. */
function GamePanel({ series }: { series: Series }) {
  const refresh = useRefreshSeries();
  const pending = pendingGame(series);
  const number = series.games.length + (pending ? 0 : 1);
  const [side, setSide] = useState<string>(series.alternateSides ? "auto" : "blue");
  const play = $api.useMutation("post", "/series/{id}/games", { onSuccess: refresh });
  const result = $api.useMutation("put", "/games/{id}/result", { onSuccess: refresh });

  function onPlay(event: FormEvent) {
    event.preventDefault();
    play.mutate({
      params: { path: { id: series.seriesId } },
      body: { teamASide: side === "auto" ? null : side },
    });
  }

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
              <div className="flex flex-wrap gap-2">
                {(["A", "B"] as const).map((winner) => (
                  <Button
                    key={winner}
                    disabled={result.isPending}
                    onClick={() => result.mutate({ params: { path: { id: pending.id } }, body: { winner } })}
                  >
                    Victoire Équipe {winner}
                  </Button>
                ))}
              </div>
              {result.isError && <ErrorAlert error={result.error} />}
            </>
          ) : (
            <form onSubmit={onPlay} className="flex flex-wrap items-end gap-3">
              <div className="flex min-w-56 flex-col gap-2">
                <Label htmlFor="side-equipe-a">Side de l'Équipe A</Label>
                <Select value={side} onValueChange={setSide}>
                  <SelectTrigger id="side-equipe-a" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {series.alternateSides && <SelectItem value="auto">Alternance automatique</SelectItem>}
                    <SelectItem value="blue">Blue side (B en Red)</SelectItem>
                    <SelectItem value="red">Red side (B en Blue)</SelectItem>
                  </SelectContent>
                </Select>
              </div>
              <Button type="submit" disabled={play.isPending}>
                Lancer la game {number}
              </Button>
              {play.isError && (
                <div className="w-full">
                  <ErrorAlert error={play.error} />
                </div>
              )}
            </form>
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
      await navigate({ to: "/series/$seriesId", params: { seriesId: String(created.seriesId) } });
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
            initial={{ format: series.format, fearless: series.fearless, alternateSides: series.alternateSides }}
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

function GamesList({ series }: { series: Series }) {
  if (series.games.length === 0) return null;

  return (
    <section aria-labelledby="games-serie" className="flex flex-col gap-3">
      <h2 id="games-serie" className="text-lg font-semibold">
        Games
      </h2>
      <ol className="flex flex-col gap-2">
        {series.games.map((g, index) => (
          <li key={g.id} className="flex flex-col gap-2 rounded-lg border px-3 py-2 text-sm">
            <div className="flex flex-wrap items-center justify-between gap-2">
              <span>
                Game {index + 1} · Équipe A <span className={sideTextClass(g.teamASide)}>{sideLabel(g.teamASide)}</span> ·
                Équipe B <span className={sideTextClass(g.teamBSide)}>{sideLabel(g.teamBSide)}</span>
              </span>
              {g.winner ? (
                <Badge variant="secondary">Équipe {g.winner} gagne</Badge>
              ) : (
                <Badge variant="outline">En attente</Badge>
              )}
            </div>
            {g.status === "completed" && <DraftChampionsTerminee gameId={g.id} />}
          </li>
        ))}
      </ol>
    </section>
  );
}
