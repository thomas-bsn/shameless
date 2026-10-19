import { useQueryClient } from "@tanstack/react-query";
import { $api, type Schemas } from "@/api/client";
import { DraftChampionsView, useMaintenant } from "@/components/draft-champions-view";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { mainALOrganisateur } from "@/lib/draft-champions";
import { POLLING_DRAFT_MS, useChampions } from "@/lib/draft-champions-queries";
import { problemStatus } from "@/lib/problem";

type Series = Schemas["SeriesResponse"];
type Draft = Schemas["DraftChampionsResponse"];


/**
 * La Draft de champions de la game en attente, côté organisateur : elle s'ouvre quand les deux
 * Capitaines sont prêts ; il la suit en direct, et joue le tour à la place de l'Équipe attendue une
 * fois son échéance atteinte.
 */
export function DraftChampionsOrganisateur({ series, gameId }: { series: Series; gameId: number }) {
  const queryClient = useQueryClient();
  const params = { params: { path: { gameId } } };
  const draft = $api.useQuery("get", "/games/{gameId}/draft-champions", params, {
    retry: false,
    refetchInterval: (query) => (query.state.data && !query.state.data.terminee ? POLLING_DRAFT_MS : false),
  });
  const onDraft = (data: Draft) => queryClient.setQueryData(["get", "/games/{gameId}/draft-champions", params], data);
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
            <DraftPasOuverte series={series} />
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

/**
 * La Draft de champions d'une game terminée, en lecture seule, repliée sous la game. Une game sans
 * Draft de champions (404) n'affiche rien.
 */
export function DraftChampionsTerminee({ gameId }: { gameId: number }) {
  const draft = $api.useQuery("get", "/games/{gameId}/draft-champions", { params: { path: { gameId } } }, { retry: false });
  const champions = useChampions();

  if (draft.isPending || (draft.isError && problemStatus(draft.error) === 404)) return null;
  if (draft.isError) return <ErrorAlert error={draft.error} />;

  return (
    <details>
      <summary className="cursor-pointer text-muted-foreground hover:text-foreground">Draft de champions</summary>
      <div className="mt-3">
        <DraftChampionsView draft={draft.data} champions={champions.data ?? []} equipe={null} jeu={null} avecEquipes={false} />
      </div>
    </details>
  );
}

/** La game n'a pas encore de draft : elle s'ouvre quand les deux Capitaines sont prêts. */
function DraftPasOuverte({ series }: { series: Series }) {
  const capitaines = series.capitaineA !== null && series.capitaineB !== null;

  return (
    <p className="text-sm text-muted-foreground">
      {capitaines
        ? "La Draft de champions s'ouvre quand les deux Capitaines sont prêts, depuis leur Lien capitaine."
        : "Désignez le capitaine de chaque Équipe : la Draft s'ouvre quand les deux sont prêts."}
    </p>
  );
}

/** La Draft en cours ou terminée ; passé l'échéance du tour, l'organisateur joue à la place de l'Équipe attendue. */
function DraftEnDirect({ draft, onDraft }: { draft: Draft; onDraft: (data: Draft) => unknown }) {
  const queryClient = useQueryClient();
  const champions = useChampions();
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
