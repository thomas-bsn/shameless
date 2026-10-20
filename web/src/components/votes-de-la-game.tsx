import { $api, type Schemas } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { cn } from "@/lib/utils";
import { AvatarJoueur } from "@/components/avatar-joueur";

const heureLabel = (iso: string) => new Date(iso).toLocaleTimeString("fr-FR", { hour: "2-digit", minute: "2-digit" });

/** Les votes DOFD d'une Game, déposés dans Discord : statut, gagnants et décompte des voix. */
export function VotesDeLaGame({ gameId }: { gameId: number }) {
  const votes = $api.useQuery("get", "/games/{id}/votes", { params: { path: { id: gameId } } });

  return (
    <section aria-labelledby="votes-titre" className="flex flex-col gap-3">
      <h2 id="votes-titre" className="text-lg font-semibold">
        Votes
      </h2>
      {votes.isPending ? (
        <LoadingRows />
      ) : votes.isError ? (
        <ErrorAlert error={votes.error} />
      ) : (
        <Resultats votes={votes.data} />
      )}
    </section>
  );
}

function Resultats({ votes }: { votes: Schemas["ResultatsDesVotes"] }) {
  if (votes.statut === "a_venir")
    return <p className="text-sm text-muted-foreground">Les votes ouvriront dans Discord à la fin de la Game.</p>;

  const votants = `${votes.votants} votant${votes.votants > 1 ? "s" : ""}`;
  if (votes.statut === "ouverts")
    return (
      <>
        <p className="text-sm text-muted-foreground">
          {votes.fermetureLe
            ? `Votes ouverts dans Discord jusqu'à ${heureLabel(votes.fermetureLe)} · ${votants}`
            : `Votes ouverts dans Discord · ${votants}`}
        </p>
        <p className="text-sm text-muted-foreground">Les gagnants et le décompte des voix seront dévoilés à la clôture.</p>
      </>
    );

  return (
    <>
      <p className="text-sm text-muted-foreground">{`Votes clos · ${votants}`}</p>
      <div className="grid gap-4 md:grid-cols-2">
        <Titre titre="DOFD" resultat={votes.dofd} />
      </div>
    </>
  );
}

function Titre({ titre, resultat }: { titre: string; resultat: Schemas["ResultatDuTitre"] }) {
  return (
    <Card role="group" aria-label={titre}>
      <CardHeader>
        <CardTitle className="text-sm">{titre}</CardTitle>
      </CardHeader>
      <CardContent>
        {resultat.decompte.length === 0 ? (
          <p className="text-sm text-muted-foreground">Aucun vote</p>
        ) : (
          <ul className="flex flex-col gap-2">
            {resultat.decompte.map((v) => {
              const gagnant = resultat.gagnants.includes(v.playerId);
              return (
                <li key={v.playerId} className="flex items-center justify-between gap-2 text-sm">
                  <span className={cn("flex items-center gap-2", gagnant && "font-semibold")}>
                    <AvatarJoueur prenom={v.prenom} avatarUrl={v.avatarUrl} className="size-6 text-[10px]" />
                    <span>{v.prenom}</span>
                    {gagnant && <Badge variant="secondary">Gagnant</Badge>}
                  </span>
                  <span className="text-xs text-muted-foreground">{v.voix} voix</span>
                </li>
              );
            })}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
