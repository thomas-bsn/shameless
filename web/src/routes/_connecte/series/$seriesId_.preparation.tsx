import { createFileRoute, Link } from "@tanstack/react-router";
import { $api } from "@/api/client";
import { PreparationDeSerie } from "@/components/preparation-de-serie";
import { ErrorAlert, LoadingRows } from "@/components/query-state";

export const Route = createFileRoute("/_connecte/series/$seriesId_/preparation")({
  component: PreparationPage,
});

function PreparationPage() {
  const { seriesId } = Route.useParams();
  const id = Number(seriesId);
  const series = $api.useQuery("get", "/series/{id}", { params: { path: { id } } });

  return (
    <div className="flex max-w-4xl flex-col gap-6">
      <Link to="/series/$seriesId" params={{ seriesId }} className="text-sm text-muted-foreground hover:text-foreground">
        ← Série
      </Link>
      {series.isPending ? (
        <LoadingRows />
      ) : series.isError ? (
        <ErrorAlert error={series.error} />
      ) : (
        <PreparationDeSerie series={series.data} />
      )}
    </div>
  );
}
