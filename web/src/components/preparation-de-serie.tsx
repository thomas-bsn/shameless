import { useQueryClient } from "@tanstack/react-query";
import { RefreshCw } from "lucide-react";
import { $api, type Schemas } from "@/api/client";
import { CodeCompanionPanel } from "@/components/code-companion-panel";
import { CopyField } from "@/components/copy-field";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { TeamCard } from "@/components/team-card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { lienCapitaineUrl, lienSpectateurUrl } from "@/lib/liens";

type Series = Schemas["SeriesResponse"];
type Equipe = "A" | "B";

const equipes = ["A", "B"] as const;

/**
 * L'Écran de préparation (ADR 0017), réservé à l'organisateur : les Équipes, le Capitaine de chacune
 * avec son Lien capitaine, le Lien spectateur et le Code Companion. Les secrets ne se trouvent qu'ici.
 */
export function PreparationDeSerie({ series }: { series: Series }) {
  const queryClient = useQueryClient();
  const params = { params: { path: { id: series.seriesId } } };
  const liens = $api.useQuery("get", "/series/{id}/liens", params);
  const renouveler = $api.useMutation("post", "/series/{id}/liens/{equipe}/renouveler", {
    onSuccess: (data: Schemas["LiensSerieResponse"]) =>
      queryClient.setQueryData(["get", "/series/{id}/liens", params], data),
  });

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold">Prépare la série</h1>
        <div className="mt-2 flex flex-wrap gap-2">
          <Badge variant="outline">BO{series.format}</Badge>
          {series.fearless && <Badge variant="outline">Fearless</Badge>}
        </div>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <TeamCard equipe="A" players={series.teamA} />
        <TeamCard equipe="B" players={series.teamB} />
      </div>

      <section aria-labelledby="capitaines">
        <Card>
          <CardHeader>
            <CardTitle id="capitaines">Capitaines</CardTitle>
          </CardHeader>
          <CardContent className="grid gap-6 sm:grid-cols-2">
            {equipes.map((equipe) => (
              <div key={equipe} className="flex flex-col gap-4">
                <CapitaineSelect series={series} equipe={equipe} />
                {liens.isPending ? (
                  <LoadingRows />
                ) : liens.isError ? (
                  <ErrorAlert error={liens.error} />
                ) : (
                  <CopyField
                    id={`lien-capitaine-${equipe}`}
                    label={`Lien capitaine de l'Équipe ${equipe}`}
                    value={lienCapitaineUrl(equipe === "A" ? liens.data.lienCapitaineA : liens.data.lienCapitaineB)}
                  >
                    <Button
                      variant="outline"
                      disabled={renouveler.isPending}
                      onClick={() => {
                        if (window.confirm(`Renouveler le Lien capitaine de l'Équipe ${equipe} ? L'ancien ne marchera plus.`))
                          renouveler.mutate({ params: { path: { id: series.seriesId, equipe } } });
                      }}
                    >
                      <RefreshCw />
                      Renouveler
                    </Button>
                  </CopyField>
                )}
              </div>
            ))}
            {renouveler.isError && <ErrorAlert error={renouveler.error} />}
          </CardContent>
        </Card>
      </section>

      <section aria-labelledby="pour-les-autres">
        <Card>
          <CardHeader>
            <CardTitle id="pour-les-autres">Pour les autres</CardTitle>
          </CardHeader>
          <CardContent>
            {liens.isSuccess && (
              <CopyField
                id="lien-spectateur"
                label="Lien spectateur"
                value={lienSpectateurUrl(liens.data.lienSpectateur)}
              />
            )}
          </CardContent>
        </Card>
      </section>

      <CodeCompanionPanel seriesId={series.seriesId} />
    </div>
  );
}

/**
 * Désigne ou change le Capitaine d'une Équipe parmi ses joueurs. Les deux Équipes n'ont aucun joueur en
 * commun : le même joueur ne peut donc pas mener les deux (l'API le refuse aussi).
 */
function CapitaineSelect({ series, equipe }: { series: Series; equipe: Equipe }) {
  const queryClient = useQueryClient();
  const players = equipe === "A" ? series.teamA : series.teamB;
  const capitaineId = equipe === "A" ? series.capitaineA : series.capitaineB;
  const designer = $api.useMutation("put", "/series/{id}/capitaines/{equipe}", {
    onSuccess: (data: Series) =>
      queryClient.setQueryData(["get", "/series/{id}", { params: { path: { id: series.seriesId } } }], data),
  });

  return (
    <div className="flex flex-col gap-2">
      <Label htmlFor={`capitaine-${equipe}`}>Capitaine de l'Équipe {equipe}</Label>
      <Select
        value={capitaineId == null ? "" : String(capitaineId)}
        disabled={designer.isPending}
        onValueChange={(value) =>
          designer.mutate({
            params: { path: { id: series.seriesId, equipe } },
            body: { designation: "Manuel", playerId: Number(value) },
          })
        }
      >
        <SelectTrigger id={`capitaine-${equipe}`} className="w-full">
          <SelectValue placeholder="Choisir…" />
        </SelectTrigger>
        <SelectContent>
          {players.map((p) => (
            <SelectItem key={p.playerId} value={String(p.playerId)}>
              {p.prenom}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
      {designer.isError && <ErrorAlert error={designer.error} />}
    </div>
  );
}
