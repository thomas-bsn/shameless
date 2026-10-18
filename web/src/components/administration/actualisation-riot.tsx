import { useQueryClient } from "@tanstack/react-query";
import { RefreshCwIcon } from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { $api } from "@/api/client";
import { ErrorAlert } from "@/components/query-state";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";

const SANS_CLE =
  "Aucune clé Riot n'est configurée sur cette Instance : l'actualisation est indisponible.";
const POLLING_MS = 1000;

/** L'état de l'actualisation Riot, relu toutes les secondes tant qu'un lancement tourne. */
export function useActualisationRiot() {
  const queryClient = useQueryClient();
  const etat = $api.useQuery("get", "/admin/riot", undefined, {
    refetchInterval: (query) =>
      query.state.data?.enCours ? POLLING_MS : false,
  });
  const enCours = etat.data?.enCours ?? false;
  const etaitEnCours = useRef(false);

  // Quand un lancement se termine, les rangs et rôles affichés sont relus.
  useEffect(() => {
    if (etaitEnCours.current && !enCours)
      void queryClient.invalidateQueries({ queryKey: ["get", "/players"] });
    etaitEnCours.current = enCours;
  }, [enCours, queryClient]);

  return {
    etat: etat.data,
    /** Faux sans clé Riot ou pendant un lancement : les boutons sont grisés. */
    peutLancer: (etat.data?.disponible ?? false) && !enCours,
    rafraichir: () =>
      queryClient.invalidateQueries({ queryKey: ["get", "/admin/riot"] }),
  };
}

/** Le bouton « Actualiser » d'une ligne de joueur. */
export function BoutonActualiserJoueur({
  playerId,
  prenom,
}: {
  playerId: number;
  prenom: string;
}) {
  const queryClient = useQueryClient();
  const { peutLancer, rafraichir } = useActualisationRiot();
  const actualiser = $api.useMutation(
    "post",
    "/admin/riot/joueurs/{playerId}",
    {
      onSettled: async () => {
        await queryClient.invalidateQueries({ queryKey: ["get", "/players"] });
        await rafraichir();
      },
    },
  );

  return (
    <div className="flex flex-col items-start gap-1">
      <Button
        variant="outline"
        size="sm"
        aria-label={`Actualiser ${prenom}`}
        disabled={!peutLancer || actualiser.isPending}
        onClick={() => actualiser.mutate({ params: { path: { playerId } } })}
      >
        <RefreshCwIcon />
        Actualiser
      </Button>
      {actualiser.isError && <ErrorAlert error={actualiser.error} />}
      {actualiser.data?.echecs.map((echec) => (
        <p key={echec} className="text-xs text-amber-500">
          {echec}
        </p>
      ))}
    </div>
  );
}

/** Le bandeau « Tout actualiser » avec la progression de la tâche de fond. */
export function ActualisationRiotPanel() {
  const { etat, peutLancer, rafraichir } = useActualisationRiot();
  const [erreur, setErreur] = useState<unknown>(null);
  const lancer = $api.useMutation("post", "/admin/riot/tous", {
    onMutate: () => setErreur(null),
    onError: setErreur,
    onSettled: rafraichir,
  });

  if (!etat) return null;
  const aTourne = etat.nature !== null;

  return (
    <Card>
      <CardHeader>
        <CardTitle>Actualisation Riot</CardTitle>
        <CardDescription>
          Relit les rangs SoloQ et Flex et recalcule le rôle principal (15
          dernières parties de SoloQ). Si Riot échoue pour un joueur, ses
          anciennes valeurs sont conservées.
        </CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-3">
          <Button
            disabled={!peutLancer || lancer.isPending}
            onClick={() => lancer.mutate({})}
          >
            <RefreshCwIcon />
            Tout actualiser
          </Button>
          {!etat.disponible && (
            <p className="text-sm text-muted-foreground">{SANS_CLE}</p>
          )}
        </div>
        {erreur !== null && <ErrorAlert error={erreur} />}
        {etat.enCours && (
          <div className="flex flex-col gap-1" role="status">
            <p className="text-sm">
              {etat.nature === "Tous les joueurs"
                ? `${etat.termines} / ${etat.total} joueurs`
                : `${etat.nature} en cours…`}
            </p>
            {etat.total > 0 && (
              <progress
                className="h-2 w-full accent-primary"
                max={etat.total}
                value={etat.termines}
              />
            )}
          </div>
        )}
        {!etat.enCours && aTourne && etat.nature === "Tous les joueurs" && (
          <div className="flex flex-col gap-1" role="status">
            <p className="text-sm">
              Actualisation terminée : {etat.termines} joueurs,{" "}
              {etat.echecs.length} échec{etat.echecs.length > 1 ? "s" : ""}.
            </p>
            {etat.echecs.length > 0 && (
              <ul className="list-disc pl-5 text-sm text-amber-500">
                {etat.echecs.map((echec) => (
                  <li key={`${echec.playerId}-${echec.message}`}>
                    {echec.prenom} : {echec.message}
                  </li>
                ))}
              </ul>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
