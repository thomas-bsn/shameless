import { createFileRoute, Link } from "@tanstack/react-router";
import { $api } from "@/api/client";
import { PageHeader } from "@/components/page-header";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Card, CardContent } from "@/components/ui/card";

export const Route = createFileRoute("/_connecte/hall-de-la-honte")({
  component: HallDeLaHonte,
});

/** Le cumul historique des titres de DOFD, sans remise à zéro mensuelle ; les ex æquo partagent le rang. */
function HallDeLaHonte() {
  const hall = $api.useQuery("get", "/hall-de-la-honte");

  return (
    <>
      <PageHeader title="Hall de la Honte" description="Le cumul des titres de DOFD depuis la première game" />
      {hall.isPending ? (
        <LoadingRows />
      ) : hall.isError ? (
        <ErrorAlert error={hall.error} />
      ) : hall.data.joueurs.length === 0 ? (
        <p className="text-muted-foreground">Aucun DOFD élu pour l'instant.</p>
      ) : (
        <Card>
          <CardContent>
            <ol aria-label="Hall de la Honte" className="divide-y">
              {hall.data.joueurs.map((ligne) => (
                <li key={ligne.playerId} className="flex items-center gap-4 py-2">
                  <span aria-label="Rang" className="w-8 text-right text-lg font-bold tabular-nums text-muted-foreground">
                    {ligne.rang}
                  </span>
                  <Link
                    to="/joueurs/$playerId"
                    params={{ playerId: String(ligne.playerId) }}
                    className="flex-1 font-medium hover:underline"
                  >
                    {ligne.prenom} <span className="text-sm font-normal text-muted-foreground">{ligne.riotId}</span>
                  </Link>
                  <span className="font-semibold tabular-nums">{ligne.dofd} DOFD</span>
                </li>
              ))}
            </ol>
          </CardContent>
        </Card>
      )}
    </>
  );
}
