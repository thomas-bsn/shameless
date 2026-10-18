import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link } from "@tanstack/react-router";
import { $api, type Schemas } from "@/api/client";
import { PageHeader } from "@/components/page-header";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { pickerProgress } from "@/lib/picker";
import { cn } from "@/lib/utils";

export const Route = createFileRoute("/_connecte/soiree/")({
  component: SoireePage,
});

type Series = Schemas["SeriesResponse"];

const openedLabel = (iso: string) =>
  new Date(iso).toLocaleTimeString("fr-FR", { hour: "2-digit", minute: "2-digit" }).replace(":", " h ");

function SoireePage() {
  const queryClient = useQueryClient();
  const open = $api.useQuery("get", "/soirees/open");
  const refresh = () => queryClient.invalidateQueries({ queryKey: ["get", "/soirees/open"] });
  const ouvrir = $api.useMutation("post", "/soirees", { onSuccess: refresh });
  const fermer = $api.useMutation("post", "/soirees/{id}/close", { onSuccess: refresh });

  if (open.isPending) return <LoadingRows />;
  if (open.isError) return <ErrorAlert error={open.error} />;

  const soiree = open.data.soiree;
  if (!soiree) {
    return (
      <div className="flex max-w-4xl flex-col gap-6">
        <PageHeader title="Aucune soirée ouverte" description="Soirée" />
        {ouvrir.isError && <ErrorAlert error={ouvrir.error} />}
        <Card>
          <CardContent className="flex flex-col items-center gap-4 py-10 text-center">
            <p className="text-muted-foreground">Ouvre une soirée pour former les équipes et jouer des séries.</p>
            <Button size="lg" className="h-12 px-8 text-base" disabled={ouvrir.isPending} onClick={() => ouvrir.mutate({})}>
              Ouvrir une soirée
            </Button>
            <p className="text-xs text-muted-foreground">Elle se ferme à la main, ou toute seule après un moment sans game.</p>
          </CardContent>
        </Card>
      </div>
    );
  }

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <PageHeader
        title="Ce soir"
        description={`Soirée · ouverte à ${openedLabel(soiree.openedAt)}`}
        actions={
          <Button
            variant="ghost"
            disabled={fermer.isPending}
            onClick={() => {
              if (window.confirm("Fermer la soirée ? Les séries en cours resteront telles quelles."))
                fermer.mutate({ params: { path: { id: soiree.id } } });
            }}
          >
            Fermer la soirée
          </Button>
        }
      />
      {fermer.isError && <ErrorAlert error={fermer.error} />}
      <SoireeEnCours soireeId={soiree.id} picker={soiree.picker} />
    </div>
  );
}

function SoireeEnCours({ soireeId, picker }: { soireeId: number; picker: Schemas["PickerResponse"] | null }) {
  const series = $api.useQuery("get", "/soirees/{id}/series", { params: { path: { id: soireeId } } });

  if (series.isPending) return <LoadingRows />;
  if (series.isError) return <ErrorAlert error={series.error} />;

  const liste = series.data.series;
  const indexEnCours = liste.findLastIndex((s) => s.outcome === "EnCours");

  return (
    <>
      {indexEnCours >= 0 && <FaceAFace series={liste[indexEnCours]} index={indexEnCours + 1} />}
      <div className="grid gap-4 sm:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Prochaine série</CardTitle>
          </CardHeader>
          <CardContent className="flex flex-col items-start gap-3">
            <p className="text-sm text-muted-foreground">
              {picker ? `Picker en cours · ${pickerProgress(picker)}` : "Aucun Picker en cours : forme les équipes."}
            </p>
            <Button asChild variant="secondary">
              <Link to="/soiree/picker">{picker ? "Reprendre le Picker" : "Former les équipes"}</Link>
            </Button>
          </CardContent>
        </Card>
        <Deroule series={liste} indexEnCours={indexEnCours} />
      </div>
    </>
  );
}

function FaceAFace({ series, index }: { series: Series; index: number }) {
  const gameIndex = series.games.findIndex((g) => g.status === "pending");
  const pending = series.games[gameIndex];

  return (
    <Card role="region" aria-label="Série en cours">
      <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <CardTitle>Série {index} · en cours</CardTitle>
          <Badge variant="outline">BO{series.format}</Badge>
          {series.fearless && <Badge variant="outline">Fearless</Badge>}
        </div>
        <Button asChild>
          {pending ? (
            <Link to="/games/$gameId" params={{ gameId: String(pending.id) }}>
              Voir la game {gameIndex + 1}
            </Link>
          ) : (
            <Link to="/series/$seriesId" params={{ seriesId: String(series.seriesId) }}>
              Voir la série
            </Link>
          )}
        </Button>
      </CardHeader>
      <CardContent className="grid items-center gap-4 sm:grid-cols-[1fr_auto_1fr]">
        <Equipe nom="Équipe 1" joueurs={series.teamA} className="sm:items-end" />
        <p aria-label="Score de la série" className="text-center font-mono text-5xl font-bold tabular-nums">
          {series.teamAWins} – {series.teamBWins}
        </p>
        <Equipe nom="Équipe 2" joueurs={series.teamB} />
      </CardContent>
    </Card>
  );
}

function Equipe({ nom, joueurs, className }: { nom: string; joueurs: Schemas["GameParticipantDto"][]; className?: string }) {
  return (
    <div className={cn("flex flex-col gap-2", className)}>
      <h3 className="text-sm font-semibold">{nom}</h3>
      <ul className="flex flex-wrap gap-1.5 sm:justify-end">
        {joueurs.map((j) => (
          <li key={j.playerId}>
            <Badge variant="secondary">{j.prenom}</Badge>
          </li>
        ))}
      </ul>
    </div>
  );
}

function Deroule({ series, indexEnCours }: { series: Series[]; indexEnCours: number }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Déroulé de la soirée</CardTitle>
      </CardHeader>
      <CardContent>
        {series.length === 0 ? (
          <p className="text-sm text-muted-foreground">Aucune série pour l'instant.</p>
        ) : (
          <ol aria-label="Déroulé de la soirée" className="flex flex-col gap-2">
            {series.map((s, i) => (
              <li key={s.seriesId} aria-current={i === indexEnCours ? "step" : undefined}>
                <Link
                  to="/series/$seriesId"
                  params={{ seriesId: String(s.seriesId) }}
                  className={cn(
                    "flex items-center justify-between gap-2 rounded-md border px-3 py-2 text-sm hover:bg-accent",
                    i === indexEnCours ? "border-foreground font-medium" : "text-muted-foreground",
                  )}
                >
                  <span>
                    Série {i + 1} · BO{s.format}
                  </span>
                  <span className="font-mono tabular-nums">
                    {s.teamAWins} – {s.teamBWins}
                  </span>
                </Link>
              </li>
            ))}
          </ol>
        )}
      </CardContent>
    </Card>
  );
}
