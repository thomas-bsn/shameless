import { $api, type Schemas } from "@/api/client";
import { ErrorAlert, LoadingRows } from "@/components/query-state";
import { AvatarJoueur } from "@/components/avatar-joueur";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";

type Pronostic = Schemas["PronosticEvalue"];

/** Le nom affiché d'un Spectateur : son nom Discord, sinon la fin de son ID Discord. */
const nomDe = (p: Pronostic) => p.pronostiqueurNom ?? `Spectateur ${p.pronostiqueurDiscordId.slice(-4)}`;

/**
 * Les pronostics des Spectateurs sur une Game, déposés dans Discord avant son lancement : chaque choix
 * est juste, raté ou en attente de son résultat. Ni points ni classement : listés par nom.
 */
export function PronosticsDeLaGame({ gameId }: { gameId: number }) {
  const pronostics = $api.useQuery("get", "/games/{id}/pronostics", { params: { path: { id: gameId } } });

  return (
    <section aria-labelledby="pronostics-titre" className="flex flex-col gap-3">
      <h2 id="pronostics-titre" className="text-lg font-semibold">
        Pronostics
      </h2>
      {pronostics.isPending ? (
        <LoadingRows />
      ) : pronostics.isError ? (
        <ErrorAlert error={pronostics.error} />
      ) : pronostics.data.pronostics.length === 0 ? (
        <p className="text-sm text-muted-foreground">Aucun pronostic pour cette Game.</p>
      ) : (
        <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-3">
          {pronostics.data.pronostics.map((p) => (
            <Card key={p.pronostiqueurDiscordId} role="group" aria-label={`Pronostic de ${nomDe(p)}`}>
              <CardHeader>
                <CardTitle className="text-sm">{nomDe(p)}</CardTitle>
              </CardHeader>
              <CardContent>
                <ul className="flex flex-col gap-2">
                  {p.equipeGagnante && (
                    <Choix label={`Vainqueur : Équipe ${p.equipeGagnante.equipe}`} correct={p.equipeGagnante.correct} />
                  )}
                  {p.dofd && <Choix label={`DOFD : ${p.dofd.prenom}`} joueur={p.dofd} correct={p.dofd.correct} />}
                </ul>
              </CardContent>
            </Card>
          ))}
        </div>
      )}
    </section>
  );
}

function Choix({ label, joueur, correct }: { label: string; joueur?: { prenom: string; avatarUrl?: string | null }; correct: boolean | null }) {
  return (
    <li className="flex items-center justify-between gap-2 text-sm">
      <span className="flex items-center gap-2">
        {joueur && <AvatarJoueur prenom={joueur.prenom} avatarUrl={joueur.avatarUrl} className="size-5 text-[9px]" />}
        {label}
      </span>
      {correct === null ? (
        <Badge variant="outline">En attente</Badge>
      ) : correct ? (
        <Badge variant="secondary">Juste</Badge>
      ) : (
        <Badge variant="destructive">Raté</Badge>
      )}
    </li>
  );
}
