import { useQueryClient } from "@tanstack/react-query";
import { $api, type Schemas } from "@/api/client";
import { DraftChampionsView, useMaintenant } from "@/components/draft-champions-view";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { mainALOrganisateur } from "@/lib/draft-champions";
import { problemStatus } from "@/lib/problem";

type Series = Schemas["SeriesResponse"];
type Draft = Schemas["DraftChampionsResponse"];

/** Rafraîchissement de la Draft de champions en cours (polling). */
const POLLING_DRAFT_MS = 1500;

/**
 * La Draft de champions de la game en attente, côté organisateur : il l'ouvre une fois le Side
 * connu et les deux capitaines désignés, la suit en direct, et joue le tour à la place de l'Équipe
 * attendue une fois son échéance atteinte.
 */
export function DraftChampionsOrganisateur({ series, gameId }: { series: Series; gameId: number }) {
  const queryClient = useQueryClient();
  const params = { params: { path: { gameId } } };
  const draft = $api.useQuery("get", "/games/{gameId}/draft-champions", params, {
    retry: false,
    refetchInterval: (query) => (query.state.data && !query.state.data.terminee ? POLLING_DRAFT_MS : false),
  });
  const onDraft = (data: Draft) => queryClient.setQueryData(["get", "/games/{gameId}/draft-champions", params], data);
  const ouvrir = $api.useMutation("post", "/series/{id}/draft-champions", { onSuccess: onDraft });
  const nonOuverte = draft.isError && !draft.data && problemStatus(draft.error) === 404;

  return (
    <section aria-labelledby="draft-champions">
      <Card>
        <CardHeader>
          <CardTitle id="draft-champions">Draft de champions</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {draft.isPending ? (
            <LoadingRows />
          ) : nonOuverte ? (
            <OuvrirDraft
              series={series}
              pending={ouvrir.isPending}
              error={ouvrir.isError ? ouvrir.error : null}
              onOuvrir={() => ouvrir.mutate({ params: { path: { id: series.seriesId } } })}
            />
          ) : draft.data ? (
            <>
              {draft.isError && <ErrorAlert error={draft.error} />}
              <DraftEnDirect draft={draft.data} onDraft={onDraft} />
            </>
          ) : (
            <ErrorAlert error={draft.error} />
          )}
        </CardContent>
      </Card>
    </section>
  );
}

/** Ouvre la Draft de champions de la game en attente : il faut un capitaine par Équipe. */
function OuvrirDraft({
  series,
  pending,
  error,
  onOuvrir,
}: {
  series: Series;
  pending: boolean;
  error: unknown;
  onOuvrir: () => void;
}) {
  const capitaines = series.capitaineA !== null && series.capitaineB !== null;

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-muted-foreground">
        {capitaines
          ? "Envoyez les Liens capitaine, puis ouvrez la Draft : chaque capitaine a 60 secondes par tour."
          : "Désignez le capitaine de chaque Équipe avant d'ouvrir la Draft de champions."}
      </p>
      <Button className="self-start" disabled={pending || !capitaines} onClick={onOuvrir}>
        Ouvrir la Draft de champions
      </Button>
      {error != null && <ErrorAlert error={error} />}
    </div>
  );
}

/** La Draft en cours ou terminée ; passé l'échéance du tour, l'organisateur joue à la place de l'Équipe attendue. */
function DraftEnDirect({ draft, onDraft }: { draft: Draft; onDraft: (data: Draft) => unknown }) {
  const queryClient = useQueryClient();
  const champions = $api.useQuery("get", "/champions", {}, { staleTime: Infinity });
  const jouer = $api.useMutation("post", "/games/{gameId}/draft-champions/actions", {
    onSuccess: onDraft,
    onSettled: () => queryClient.invalidateQueries({ queryKey: ["get", "/games/{gameId}/draft-champions"] }),
  });
  const maintenant = useMaintenant();
  const tour = draft.tourCourant;
  const aLaPlace = tour !== null && mainALOrganisateur(draft, maintenant);

  return (
    <>
      {champions.isError && <ErrorAlert error={champions.error} />}
      <DraftChampionsView
        draft={draft}
        champions={champions.data ?? []}
        equipe={null}
        avecEquipes={false}
        consigne={
          tour === null
            ? undefined
            : aLaPlace
              ? `Temps écoulé : vous pouvez jouer à la place de l'Équipe ${tour.equipe}`
              : `Au tour du capitaine de l'Équipe ${tour.equipe}`
        }
        jeu={
          aLaPlace
            ? {
                titre: `${tour.action === "ban" ? "Bannir" : "Choisir"} à la place de l'Équipe ${tour.equipe}`,
                pending: jouer.isPending,
                error: jouer.isError ? jouer.error : null,
                onJouer: (numero, championId) =>
                  jouer.mutate({ params: { path: { gameId: draft.gameId } }, body: { tour: numero, championId } }),
              }
            : null
        }
      />
    </>
  );
}
