import { $api } from "@/api/client";
import { ErrorAlert } from "@/components/query-state";
import { Skeleton } from "@/components/ui/skeleton";

const POLLING_MS = 10_000;

/**
 * Les Companions connectés à l'API : leur Joueur (retrouvé par PUUID), leur Série et « vu il y a X s ».
 * Un Companion est connecté tant que son dernier signal date de moins de 90 s ; la liste se rafraîchit seule.
 */
export function CompanionConnecteAdmin() {
  const connectes = $api.useQuery("get", "/companion/connectes", undefined, { refetchInterval: POLLING_MS });
  return (
    <section aria-labelledby="companion-connecte" className="flex flex-col gap-2">
      <h2 id="companion-connecte" className="text-lg font-semibold">
        Companion connecté
      </h2>
      {connectes.isPending ? (
        <Skeleton className="h-10 w-full" />
      ) : connectes.isError ? (
        <ErrorAlert error={connectes.error} />
      ) : connectes.data.length === 0 ? (
        <p className="text-sm text-muted-foreground">aucun Companion détecté</p>
      ) : (
        <ul className="flex flex-col gap-1">
          {connectes.data.map((c, i) => (
            <li key={`${c.seriesId}-${c.joueur?.id ?? `inconnu-${i}`}`} className="flex flex-wrap items-baseline gap-2 text-sm">
              <span className="font-medium">{c.joueur?.prenom ?? "Joueur inconnu"}</span>
              <span className="text-muted-foreground">Série {c.seriesId}</span>
              <span className="text-xs text-muted-foreground">vu il y a {c.vuIlYASecondes} s</span>
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}
