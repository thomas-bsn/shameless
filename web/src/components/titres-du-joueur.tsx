import { $api } from "@/api/client";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

/** Les titres de MVP et de DOFD remportés par un Joueur, d'après sa fiche. */
export function TitresDuJoueur({ playerId }: { playerId: number }) {
  const fiche = $api.useQuery("get", "/players/{playerId}/fiche", { params: { path: { playerId } } });

  return (
    <Card role="group" aria-label="Titres">
      <CardHeader>
        <CardTitle className="text-sm text-muted-foreground">Titres</CardTitle>
      </CardHeader>
      <CardContent>
        {fiche.isPending ? (
          <Skeleton className="h-6 w-24" />
        ) : fiche.isError ? (
          <span className="text-muted-foreground">Indisponibles</span>
        ) : (
          <span className="flex flex-wrap gap-3 font-medium">
            <span>{fiche.data.titres.mvp} MVP</span>
            <span>{fiche.data.titres.dofd} DOFD</span>
          </span>
        )}
      </CardContent>
    </Card>
  );
}
