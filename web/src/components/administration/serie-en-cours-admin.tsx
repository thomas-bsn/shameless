import { $api, type Schemas } from "@/api/client";
import { CompanionConnecteAdmin } from "@/components/administration/companion-connecte-admin";
import { CopyField } from "@/components/copy-field";
import { EtatCapitaine } from "@/components/preparation-de-serie";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { lienCapitaineUrl, lienSpectateurUrl } from "@/lib/liens";

const POLLING_MS = 3000;
const equipes = ["A", "B"] as const;

/**
 * L'onglet Série en cours, en lecture seule : le Code Companion, les Liens capitaine et spectateur et
 * l'état des Capitaines de chaque Série en cours, pour dépanner. Le renouvellement reste à l'organisateur.
 */
export function SerieEnCoursAdmin() {
  return (
    <div className="flex flex-col gap-6">
      <CompanionConnecteAdmin />
      <SeriesEnCours />
    </div>
  );
}

function SeriesEnCours() {
  const actives = $api.useQuery("get", "/series/active");
  if (actives.isPending) return <LoadingRows />;
  if (actives.isError) return <ErrorAlert error={actives.error} />;
  if (actives.data.series.length === 0)
    return <p className="text-sm text-muted-foreground">Aucune série en cours.</p>;
  return (
    <div className="flex flex-col gap-4">
      {actives.data.series.map((s) => (
        <InfosDeSerie key={s.seriesId} series={s} />
      ))}
    </div>
  );
}

function InfosDeSerie({ series }: { series: Schemas["SeriesResponse"] }) {
  const params = { params: { path: { id: series.seriesId } } };
  const liens = $api.useQuery("get", "/series/{id}/liens", params, { refetchInterval: POLLING_MS });
  const code = $api.useQuery("get", "/series/{id}/code-companion", params);
  return (
    <Card>
      <CardHeader>
        <CardTitle className="flex items-center gap-2">
          Série {series.seriesId}
          <Badge variant="outline">BO{series.format}</Badge>
          {series.fearless && <Badge variant="outline">Fearless</Badge>}
        </CardTitle>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {code.isPending ? (
          <LoadingRows />
        ) : code.isError ? (
          <ErrorAlert error={code.error} />
        ) : (
          <CopyField id={`code-companion-${series.seriesId}`} label="Code Companion" value={code.data.code} />
        )}
        {liens.isPending ? (
          <LoadingRows />
        ) : liens.isError ? (
          <ErrorAlert error={liens.error} />
        ) : (
          <>
            <div className="grid gap-6 sm:grid-cols-2">
              {equipes.map((equipe) => (
                <div key={equipe} className="flex flex-col gap-3">
                  <EtatCapitaine
                    equipe={equipe}
                    rejointLe={equipe === "A" ? liens.data.capitaineARejointLe : liens.data.capitaineBRejointLe}
                    pretLe={equipe === "A" ? liens.data.capitaineAPretLe : liens.data.capitaineBPretLe}
                  />
                  <CopyField
                    id={`lien-capitaine-${series.seriesId}-${equipe}`}
                    label={`Lien capitaine de l'Équipe ${equipe}`}
                    value={lienCapitaineUrl(equipe === "A" ? liens.data.lienCapitaineA : liens.data.lienCapitaineB)}
                  />
                </div>
              ))}
            </div>
            <CopyField
              id={`lien-spectateur-${series.seriesId}`}
              label="Lien spectateur"
              value={lienSpectateurUrl(liens.data.lienSpectateur)}
            />
          </>
        )}
      </CardContent>
    </Card>
  );
}
