import type { Schemas } from "@/api/client";
import { Initiale, Marche, PlayerLink } from "@/components/podium";
import { Card } from "@/components/ui/card";
import { placeText } from "@/lib/cost";
import { cn } from "@/lib/utils";

type Ligne = Schemas["HallDeLaHonte"]["joueurs"][number];

/** L'illustration de la bouse, qui coiffe le podium des DOFD. */
function Bouse({ className }: { className?: string }) {
  return (
    <svg role="img" aria-label="Bouse" viewBox="0 0 64 64" className={cn("text-amber-700", className)}>
      <path
        fill="currentColor"
        d="M32 6c4 4 2 8 0 10 8 1 10 7 8 11 8 1 14 6 14 13 0 9-10 16-22 16S10 49 10 40c0-7 6-12 14-13-2-4 0-10 8-11-2-2-4-6 0-10Z"
      />
      <circle cx="26" cy="38" r="3" fill="#fff" />
      <circle cx="38" cy="38" r="3" fill="#fff" />
      <circle cx="26" cy="39" r="1.4" fill="#000" />
      <circle cx="38" cy="39" r="1.4" fill="#000" />
      <path d="M26 47c3 3 9 3 12 0" fill="none" stroke="#fff" strokeWidth="2" strokeLinecap="round" />
    </svg>
  );
}

/** Une marche du podium ; le premier est plus haut, avec une bouse plus grosse. */
function MarcheDofd({ ligne, exAequo }: { ligne: Ligne; exAequo: boolean }) {
  const premier = ligne.rang === 1;
  const libelle = exAequo ? `${placeText(ligne.rang)} ex æquo` : placeText(ligne.rang);
  return (
    <Marche label={`${libelle} : ${ligne.prenom}`} premier={premier} bordure="border-amber-700/70 ring-amber-700/40">
      <span className="text-xs font-medium tracking-wide text-muted-foreground uppercase">{libelle}</span>
      <Bouse className={premier ? "size-16" : "size-12"} />
      <Initiale prenom={ligne.prenom} className="size-10 text-lg" />
      <PlayerLink playerId={ligne.playerId} prenom={ligne.prenom} className="max-w-full truncate" />
      <span className={cn("font-mono font-bold tabular-nums", premier ? "text-3xl" : "text-2xl")}>{ligne.dofd}</span>
      <span className="text-xs text-muted-foreground">DOFD</span>
    </Marche>
  );
}

/** Le classement des DOFD : podium des trois premiers, puis la liste ; les ex æquo partagent le rang. */
export function DofdRanking({ joueurs }: { joueurs: Ligne[] }) {
  if (joueurs.length === 0) return <p className="text-muted-foreground">Aucun DOFD élu pour l'instant.</p>;

  const exAequo = (ligne: Ligne) => joueurs.filter((l) => l.rang === ligne.rang).length > 1;
  const [premier, deuxieme, troisieme] = joueurs;
  const suivants = joueurs.slice(3);

  return (
    <div className="flex flex-col gap-6">
      <ol aria-label="Podium des DOFD" className="flex flex-col gap-3 sm:flex-row sm:items-end">
        {deuxieme && <MarcheDofd ligne={deuxieme} exAequo={exAequo(deuxieme)} />}
        <MarcheDofd ligne={premier!} exAequo={exAequo(premier!)} />
        {troisieme && <MarcheDofd ligne={troisieme} exAequo={exAequo(troisieme)} />}
      </ol>
      {suivants.length > 0 && (
        <Card className="py-2">
          <ol aria-label="Classement des DOFD" className="divide-y">
            {suivants.map((ligne) => (
              <li key={ligne.playerId} className="flex items-center gap-3 px-4 py-2.5">
                <span className="flex w-14 flex-col items-end">
                  <span aria-label="Rang" className="font-mono text-sm text-muted-foreground tabular-nums">
                    {ligne.rang}
                  </span>
                  {exAequo(ligne) && <span className="text-[10px] text-muted-foreground">ex æquo</span>}
                </span>
                <Initiale prenom={ligne.prenom} className="size-8 text-sm" />
                <PlayerLink playerId={ligne.playerId} prenom={ligne.prenom} className="flex-1 truncate" />
                <span className="font-mono font-semibold tabular-nums">{ligne.dofd} DOFD</span>
              </li>
            ))}
          </ol>
        </Card>
      )}
    </div>
  );
}
