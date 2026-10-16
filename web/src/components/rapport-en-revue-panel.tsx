import { Check, X } from "lucide-react";
import type { ReactNode } from "react";
import { $api, type Schemas } from "@/api/client";
import { ErrorAlert } from "@/components/query-state";
import { Alert, AlertDescription, AlertTitle } from "@/components/ui/alert";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { useChampions } from "@/lib/draft-champions-queries";
import { sideTextClass } from "@/lib/tier-styles";

type Revue = Schemas["RapportEnRevueResponse"];

const sideLabel = (side: string) => (side === "red" ? "Red side" : "Blue side");

/**
 * Le rapport du Companion en revue pour la game en attente : ses joueurs détectés ne sont pas la
 * composition de la game. L'organisateur voit l'écart, puis accepte (la game est terminée) ou
 * rejette (la game attend un autre rapport ou une saisie manuelle).
 */
export function RapportEnRevuePanel({
  revue,
  numero,
  onDecided,
}: {
  revue: Revue;
  numero: number;
  onDecided: () => unknown;
}) {
  const champions = useChampions();
  const nomDuChampion = (id: string) => champions.data?.find((c) => c.id === id)?.name ?? id;
  const params = { params: { path: { id: revue.id } } };
  const accepter = $api.useMutation("post", "/rapports/{id}/accepter", { onSuccess: onDecided });
  const rejeter = $api.useMutation("post", "/rapports/{id}/rejeter", { onSuccess: onDecided });
  const enCours = accepter.isPending || rejeter.isPending;

  return (
    <section aria-labelledby="rapport-en-revue">
      <Card className="border-amber-500/60">
        <CardHeader>
          <CardTitle id="rapport-en-revue">Rapport de la game {numero} en revue</CardTitle>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          <Alert>
            <AlertTitle>Les joueurs détectés ne correspondent pas à la composition de la game</AlertTitle>
            <AlertDescription>
              Tant que tu n'as pas décidé, la game reste en attente : ni game suivante, ni Draft de champions, ni
              saisie manuelle.
            </AlertDescription>
          </Alert>

          <div className="grid gap-4 sm:grid-cols-3">
            {revue.absents.length > 0 && (
              <Ecart titre="Joueurs attendus absents">
                {revue.absents.map((j) => (
                  <li key={j.playerId} className="font-medium">
                    {j.prenom}
                  </li>
                ))}
              </Ecart>
            )}
            {revue.inattendus.length > 0 && (
              <Ecart titre="Joueurs détectés inattendus">
                {revue.inattendus.map((j) => (
                  <li key={j.puuid} className="flex flex-col">
                    <span className="truncate font-medium">{j.joueur?.prenom ?? j.puuid}</span>
                    <span className="text-xs text-muted-foreground">
                      <span className={sideTextClass(j.side)}>{sideLabel(j.side)}</span> ·{" "}
                      <span>{nomDuChampion(j.championId)}</span>
                    </span>
                  </li>
                ))}
              </Ecart>
            )}
            {revue.mauvaisSide.length > 0 && (
              <Ecart titre="Joueurs détectés sur l'autre Side">
                {revue.mauvaisSide.map((j) => (
                  <li key={j.playerId} className="font-medium">
                    {j.prenom}
                  </li>
                ))}
              </Ecart>
            )}
          </div>

          <p className="text-sm">
            Si tu acceptes ce rapport, l'Équipe {revue.equipeGagnante} gagne la game {numero}.
          </p>
          <div className="flex flex-wrap gap-2">
            <Button disabled={enCours} onClick={() => accepter.mutate(params)}>
              <Check />
              Accepter le rapport
            </Button>
            <Button
              variant="outline"
              disabled={enCours}
              onClick={() => {
                if (window.confirm("Rejeter ce rapport ? La game attendra un autre rapport ou une saisie manuelle."))
                  rejeter.mutate(params);
              }}
            >
              <X />
              Rejeter le rapport
            </Button>
          </div>
          {accepter.isError && <ErrorAlert error={accepter.error} />}
          {rejeter.isError && <ErrorAlert error={rejeter.error} />}
        </CardContent>
      </Card>
    </section>
  );
}

function Ecart({ titre, children }: { titre: string; children: ReactNode }) {
  return (
    <div className="flex flex-col gap-2">
      <h3 className="text-sm font-medium text-muted-foreground">{titre}</h3>
      <ul aria-label={titre} className="flex flex-col gap-1 text-sm">
        {children}
      </ul>
    </div>
  );
}
