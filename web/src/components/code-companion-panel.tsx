import { useQueryClient } from "@tanstack/react-query";
import { RefreshCw } from "lucide-react";
import { $api, type Schemas } from "@/api/client";
import { CopyField } from "@/components/copy-field";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

/**
 * Le code Companion de la Série : l'organisateur le copie pour le saisir dans le Companion, ou le
 * renouvelle (l'ancien est alors refusé).
 */
export function CodeCompanionPanel({ seriesId }: { seriesId: number }) {
  const queryClient = useQueryClient();
  const params = { params: { path: { id: seriesId } } };
  const code = $api.useQuery("get", "/series/{id}/code-companion", params);
  const renouveler = $api.useMutation("post", "/series/{id}/code-companion/renouveler", {
    onSuccess: (data: Schemas["CodeCompanionResponse"]) =>
      queryClient.setQueryData(["get", "/series/{id}/code-companion", params], data),
  });

  return (
    <section aria-labelledby="code-companion">
      <Card>
        <CardHeader>
          <CardTitle id="code-companion">Companion</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <p className="text-sm text-muted-foreground">
            Saisis ce code dans le Companion : il envoie les résultats des games de cette Série, et rien d'autre.
          </p>
          {code.isPending ? (
            <LoadingRows />
          ) : code.isError ? (
            <ErrorAlert error={code.error} />
          ) : (
            <CopyField id="code-companion-valeur" label="Code Companion" value={code.data.code}>
              <Button
                variant="outline"
                disabled={renouveler.isPending}
                onClick={() => {
                  if (window.confirm("Renouveler le code Companion ? L'ancien ne marchera plus."))
                    renouveler.mutate(params);
                }}
              >
                <RefreshCw />
                Renouveler
              </Button>
            </CopyField>
          )}
          {renouveler.isError && <ErrorAlert error={renouveler.error} />}
        </CardContent>
      </Card>
    </section>
  );
}
