import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link, useNavigate } from "@tanstack/react-router";
import { useState, type FormEvent } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { pendingGame, outcomeLabel, replacementCandidates } from "@/lib/series";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/_connecte/series/$seriesId")({
  component: SeriePage,
});

type Series = Schemas["SeriesResponse"];

const selectClass =
  "h-9 w-full rounded-md border border-input bg-transparent px-3 text-sm shadow-xs focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring";

const sideLabel = (side: string) => (side === "red" ? "Red side" : "Blue side");
const sideClass = (side: string) => (side === "red" ? "text-red-400" : "text-sky-400");

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
        <TeamCard equipe="A" players={series.teamA} />
        <TeamCard equipe="B" players={series.teamB} />
      </div>

      {enCours ? (
        <>
          <GamePanel series={series} />
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

function TeamCard({ equipe, players }: { equipe: "A" | "B"; players: Schemas["GameParticipantDto"][] }) {
  return (
    <section aria-label={`Équipe ${equipe}`}>
      <Card>
        <CardHeader>
          <CardTitle>Équipe {equipe}</CardTitle>
        </CardHeader>
        <CardContent>
          <ul className="flex flex-col gap-1 text-sm">
            {players.map((p) => (
              <li key={p.playerId} className="flex justify-between gap-2">
                <span>{p.prenom}</span>
                {p.role && <span className="text-muted-foreground">{p.role}</span>}
              </li>
            ))}
          </ul>
        </CardContent>
      </Card>
    </section>
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
                Équipe A <span className={sideClass(pending.teamASide)}>{sideLabel(pending.teamASide)}</span> · Équipe B{" "}
                <span className={sideClass(pending.teamBSide)}>{sideLabel(pending.teamBSide)}</span>
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
                <select id="side-equipe-a" className={selectClass} value={side} onChange={(e) => setSide(e.target.value)}>
                  {series.alternateSides && <option value="auto">Alternance automatique</option>}
                  <option value="blue">Blue side (B en Red)</option>
                  <option value="red">Red side (B en Blue)</option>
                </select>
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
              <select
                id="joueur-remplace"
                className={selectClass}
                value={replaced}
                onChange={(e) => setReplaced(e.target.value)}
                required
              >
                <option value="">Choisir…</option>
                {(["A", "B"] as const).map((equipe) => (
                  <optgroup key={equipe} label={`Équipe ${equipe}`}>
                    {(equipe === "A" ? series.teamA : series.teamB).map((p) => (
                      <option key={p.playerId} value={p.playerId}>
                        {p.prenom}
                      </option>
                    ))}
                  </optgroup>
                ))}
              </select>
            </div>
            <div className="flex min-w-48 flex-col gap-2">
              <Label htmlFor="joueur-remplacant">Joueur qui entre</Label>
              <select
                id="joueur-remplacant"
                className={selectClass}
                value={replacement}
                onChange={(e) => setReplacement(e.target.value)}
                required
              >
                <option value="">Choisir…</option>
                {candidates.map((p) => (
                  <option key={p.id} value={p.id}>
                    {p.prenom}
                  </option>
                ))}
              </select>
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

/** Revanche d'une Série terminée : mêmes Équipes, dans leur dernière composition. */
function RevanchePanel({ series }: { series: Series }) {
  const navigate = useNavigate();
  const refresh = useRefreshSeries();
  const [format, setFormat] = useState(String(series.format));
  const [fearless, setFearless] = useState(series.fearless);
  const [alternateSides, setAlternateSides] = useState(series.alternateSides);
  const rematch = $api.useMutation("post", "/series/{id}/rematch", {
    onSuccess: async (created) => {
      await refresh();
      await navigate({ to: "/series/$seriesId", params: { seriesId: String(created.seriesId) } });
    },
  });

  function onSubmit(event: FormEvent) {
    event.preventDefault();
    rematch.mutate({
      params: { path: { id: series.seriesId } },
      body: { format: Number(format), fearless, alternateSides },
    });
  }

  return (
    <section aria-labelledby="revanche">
      <Card>
        <CardHeader>
          <CardTitle id="revanche">Revanche</CardTitle>
        </CardHeader>
        <CardContent>
          <form onSubmit={onSubmit} className="flex flex-wrap items-end gap-4">
            <div className="flex min-w-32 flex-col gap-2">
              <Label htmlFor="format-revanche">Format</Label>
              <select id="format-revanche" className={selectClass} value={format} onChange={(e) => setFormat(e.target.value)}>
                {[1, 2, 3, 5].map((bo) => (
                  <option key={bo} value={bo}>
                    BO{bo}
                  </option>
                ))}
              </select>
            </div>
            <label className="flex h-9 items-center gap-2 text-sm">
              <input type="checkbox" checked={fearless} onChange={(e) => setFearless(e.target.checked)} />
              Fearless
            </label>
            <label className="flex h-9 items-center gap-2 text-sm">
              <input type="checkbox" checked={alternateSides} onChange={(e) => setAlternateSides(e.target.checked)} />
              Alterner les Sides
            </label>
            <Button type="submit" disabled={rematch.isPending}>
              Lancer la revanche
            </Button>
            {rematch.isError && (
              <div className="w-full">
                <ErrorAlert error={rematch.error} />
              </div>
            )}
          </form>
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
          <li key={g.id} className="flex flex-wrap items-center justify-between gap-2 rounded-lg border px-3 py-2 text-sm">
            <span>
              Game {index + 1} · Équipe A <span className={sideClass(g.teamASide)}>{sideLabel(g.teamASide)}</span> · Équipe B{" "}
              <span className={sideClass(g.teamBSide)}>{sideLabel(g.teamBSide)}</span>
            </span>
            {g.winner ? (
              <Badge variant="secondary">Équipe {g.winner} gagne</Badge>
            ) : (
              <Badge variant="outline">En attente</Badge>
            )}
          </li>
        ))}
      </ol>
    </section>
  );
}
