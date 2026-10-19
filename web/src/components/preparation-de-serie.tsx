import { useQueryClient } from "@tanstack/react-query";
import { RefreshCw } from "lucide-react";
import { $api, type Schemas } from "@/api/client";
import { AlerteDmCapitaine } from "@/components/alerte-dm-capitaine";
import { CodeCompanionPanel } from "@/components/code-companion-panel";
import { CopyField } from "@/components/copy-field";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { TeamCard } from "@/components/team-card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { lienCapitaineUrl, lienSpectateurUrl } from "@/lib/liens";

/** Fréquence de lecture de l'écran : « rejoint » et « prêt » des Capitaines et échecs de DM, comme la Draft de champions. */
const POLLING_PREPARATION_MS = 3000;

const heureLabel = (iso: string) => new Date(iso).toLocaleTimeString("fr-FR", { hour: "2-digit", minute: "2-digit" });

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
  // Relu toutes les 3 s : l'état « rejoint » et « prêt » des Capitaines et les DM que le bot n'a pas pu envoyer.
  const liens = $api.useQuery("get", "/series/{id}/liens", params, { refetchInterval: POLLING_PREPARATION_MS });
  const renouveler = $api.useMutation("post", "/series/{id}/liens/{equipe}/renouveler", {
    onSuccess: (data: Schemas["LiensSerieResponse"]) =>
      queryClient.setQueryData(["get", "/series/{id}/liens", params], data),
  });

  return (
    <div className="flex flex-col gap-6">
      <div>
        <h1 className="text-2xl font-bold">Prépare la Série</h1>
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
                <CapitaineDeLEquipe series={series} equipe={equipe} />
                {liens.isSuccess && (
                  <EtatCapitaine
                    equipe={equipe}
                    rejointLe={equipe === "A" ? liens.data.capitaineARejointLe : liens.data.capitaineBRejointLe}
                    pretLe={equipe === "A" ? liens.data.capitaineAPretLe : liens.data.capitaineBPretLe}
                  />
                )}
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
                {liens.isSuccess && <AlerteDmCapitaine equipe={equipe} liens={liens.data} />}
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
 * L'état d'un Capitaine : a-t-il rejoint (ouvert son lien) et est-il prêt ? Les deux arrivent par la
 * lecture régulière des liens ; quand les deux sont prêts, la draft démarre sans action de l'organisateur.
 */
function EtatCapitaine({ equipe, rejointLe, pretLe }: { equipe: Equipe; rejointLe: string | null; pretLe: string | null }) {
  return (
    <div className="flex flex-col gap-1">
      <Etat
        label={`État du Capitaine de l'Équipe ${equipe}`}
        ok={rejointLe != null}
        texte={rejointLe ? `Rejoint à ${heureLabel(rejointLe)}` : "Pas encore rejoint"}
      />
      <Etat
        label={`Prêt du Capitaine de l'Équipe ${equipe}`}
        ok={pretLe != null}
        texte={pretLe ? `Prêt à ${heureLabel(pretLe)}` : "Pas encore prêt"}
      />
    </div>
  );
}

function Etat({ label, ok, texte }: { label: string; ok: boolean; texte: string }) {
  return (
    <p
      role="status"
      aria-label={label}
      className={ok ? "text-sm font-medium text-emerald-600 dark:text-emerald-400" : "text-sm text-muted-foreground"}
    >
      {texte}
    </p>
  );
}

/** Le Capitaine d'une Équipe, en lecture seule : il se désigne dans le Picker. */
function CapitaineDeLEquipe({ series, equipe }: { series: Series; equipe: Equipe }) {
  const players = equipe === "A" ? series.teamA : series.teamB;
  const capitaineId = equipe === "A" ? series.capitaineA : series.capitaineB;
  const capitaine = players.find((p) => p.playerId === capitaineId);

  return (
    <div className="flex flex-col gap-1">
      <span className="text-sm font-medium">Capitaine de l'Équipe {equipe}</span>
      <p aria-label={`Capitaine de l'Équipe ${equipe}`} className={capitaine ? "" : "text-muted-foreground"}>
        {capitaine?.prenom ?? "Pas encore désigné"}
      </p>
    </div>
  );
}
