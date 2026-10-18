import { useQueryClient } from "@tanstack/react-query";
import { createFileRoute, Link } from "@tanstack/react-router";
import { $api, type Schemas } from "@/api/client";
import { PageHeader } from "@/components/page-header";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { outcomeLabel } from "@/lib/series";

export const Route = createFileRoute("/_connecte/soiree/")({
  component: SoireePage,
});

const timeLabel = (iso: string) =>
  new Date(iso).toLocaleString("fr-FR", { weekday: "long", day: "numeric", month: "long", hour: "2-digit", minute: "2-digit" });

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
        <PageHeader title="Soirée" description="Aucune Soirée n'est ouverte." />
        {ouvrir.isError && <ErrorAlert error={ouvrir.error} />}
        <Button className="self-start" disabled={ouvrir.isPending} onClick={() => ouvrir.mutate({})}>
          Ouvrir une Soirée
        </Button>
      </div>
    );
  }

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <PageHeader
        title="Soirée en cours"
        description={`Ouverte ${timeLabel(soiree.openedAt)}`}
        actions={
          <div className="flex flex-wrap gap-2">
            <Button asChild>
              <Link to="/soiree/picker">{soiree.picker ? "Reprendre le Picker" : "Former les équipes"}</Link>
            </Button>
            <Button
              variant="outline"
              disabled={fermer.isPending}
              onClick={() => {
                if (window.confirm("Fermer la Soirée ?")) fermer.mutate({ params: { path: { id: soiree.id } } });
              }}
            >
              Fermer la Soirée
            </Button>
          </div>
        }
      />
      {fermer.isError && <ErrorAlert error={fermer.error} />}
      <SeriesList soireeId={soiree.id} />
    </div>
  );
}

function SeriesList({ soireeId }: { soireeId: number }) {
  const series = $api.useQuery("get", "/soirees/{id}/series", { params: { path: { id: soireeId } } });

  if (series.isPending) return <LoadingRows />;
  if (series.isError) return <ErrorAlert error={series.error} />;
  if (series.data.series.length === 0)
    return <p className="py-12 text-center text-muted-foreground">Aucune série pour l'instant : forme les équipes.</p>;

  return (
    <section aria-labelledby="series-soiree" className="flex flex-col gap-3">
      {series.data.series.find((s) => s.outcome === "EnCours") && <SerieEnCours serie={series.data.series.find((s) => s.outcome === "EnCours")!} />}
      <h2 id="series-soiree" className="text-lg font-semibold">
        Séries
      </h2>
      <ol className="flex flex-col gap-3">
        {series.data.series.map((s, index) => (
          <li key={s.seriesId}>
            <SeriesRow series={s} index={index + 1} />
          </li>
        ))}
      </ol>
    </section>
  );
}

function SerieEnCours({ serie }: { serie: Schemas["SeriesResponse"] }) {
  const game = serie.games.find((g) => g.status === "pending");
  const noms = (team: Schemas["GameParticipantDto"][]) => team.map((p) => p.prenom).join(" · ");
  return <Card className="border-primary/40 bg-primary/5"><CardHeader className="flex flex-row items-center justify-between gap-3"><CardTitle>Face-à-face · BO{serie.format}</CardTitle><Badge>En cours</Badge></CardHeader><CardContent className="flex flex-col gap-4"><div className="grid gap-4 sm:grid-cols-[1fr_auto_1fr] sm:items-center"><div><p className="text-xs text-muted-foreground">ÉQUIPE A</p><p className="font-medium">{noms(serie.teamA)}</p></div><p className="font-mono text-2xl font-semibold">{serie.teamAWins} – {serie.teamBWins}</p><div className="sm:text-right"><p className="text-xs text-muted-foreground">ÉQUIPE B</p><p className="font-medium">{noms(serie.teamB)}</p></div></div>{game ? <Button asChild className="self-start"><Link to="/games/$gameId" params={{ gameId: String(game.id) }}>Ouvrir la Game #{game.id}</Link></Button> : <Button asChild className="self-start"><Link to="/series/$seriesId" params={{ seriesId: String(serie.seriesId) }}>Ouvrir la Série</Link></Button>}</CardContent></Card>;
}

function SeriesRow({ series, index }: { series: Schemas["SeriesResponse"]; index: number }) {
  const prenoms = (team: Schemas["GameParticipantDto"][]) => team.map((p) => p.prenom).join(", ");

  return (
    <Link to="/series/$seriesId" params={{ seriesId: String(series.seriesId) }} className="block">
      <Card className="transition-colors hover:bg-accent/40">
        <CardHeader className="flex flex-row flex-wrap items-center justify-between gap-2">
          <CardTitle className="flex items-center gap-2">
            Série {index} · BO{series.format}
            {series.fearless && <Badge variant="outline">Fearless</Badge>}
          </CardTitle>
          <div className="flex items-center gap-3">
            <span className="font-semibold">
              Équipe A {series.teamAWins} – {series.teamBWins} Équipe B
            </span>
            <Badge variant={series.outcome === "EnCours" ? "default" : "secondary"}>{outcomeLabel(series)}</Badge>
          </div>
        </CardHeader>
        <CardContent className="grid gap-1 text-sm text-muted-foreground sm:grid-cols-2">
          <p>A : {prenoms(series.teamA)}</p>
          <p>B : {prenoms(series.teamB)}</p>
        </CardContent>
      </Card>
    </Link>
  );
}
