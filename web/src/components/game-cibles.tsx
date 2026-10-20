import { Link } from "@tanstack/react-router";
import type { Schemas } from "@/api/client";
import { AvatarJoueur } from "@/components/avatar-joueur";
import type { Participant, Side } from "@/components/game-equipe-table";
import { sidesOf } from "@/components/game-scoreboard";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { sideBorderClass } from "@/lib/tier-styles";
import { cn } from "@/lib/utils";

/**
 * Les dix emplacements, du centre vers l'extérieur : 1 au centre, 3 sur le premier anneau, 6 sur le second.
 * Les positions sont écrites en classes Tailwind (centre de la pastille, en % de la cible) : le style en ligne est interdit.
 */
const EMPLACEMENTS = [
  { anneau: 0, classe: "left-[50%] top-[50%]" },
  { anneau: 1, classe: "left-[50%] top-[26%]" },
  { anneau: 1, classe: "left-[70.8%] top-[62%]" },
  { anneau: 1, classe: "left-[29.2%] top-[62%]" },
  { anneau: 2, classe: "left-[69%] top-[17.1%]" },
  { anneau: 2, classe: "left-[88%] top-[50%]" },
  { anneau: 2, classe: "left-[69%] top-[82.9%]" },
  { anneau: 2, classe: "left-[31%] top-[82.9%]" },
  { anneau: 2, classe: "left-[12%] top-[50%]" },
  { anneau: 2, classe: "left-[31%] top-[17.1%]" },
];

/** Les deux cercles tracés derrière les pastilles (diamètre en % de la cible). */
const ANNEAUX_TRACES = ["left-[26%] top-[26%] size-[48%]", "left-[12%] top-[12%] size-[76%]"];

interface Place {
  joueur: Participant;
  side: Side;
  anneau: number;
  classe: string;
}

/** Les joueurs notés, du meilleur au moins bon, chacun sur l'emplacement de sa place. */
function placer(game: Schemas["GameRecapResponse"]): Place[] {
  return sidesOf(game)
    .flatMap((s) => s.players.map((joueur) => ({ joueur, side: s.side })))
    .filter((p) => p.joueur.place != null)
    .sort((a, b) => a.joueur.place! - b.joueur.place!)
    .slice(0, EMPLACEMENTS.length)
    .map((p, i) => ({ ...p, ...EMPLACEMENTS[i]! }));
}

/** La vue en cibles : le meilleur de la game au centre, les moins bonnes notes vers l'extérieur. */
export function VueEnCibles({ game }: { game: Schemas["GameRecapResponse"] }) {
  const places = placer(game);

  if (places.length === 0) {
    return (
      <Alert>
        <AlertDescription>
          <strong>Pas de notes pour cette game.</strong> Les joueurs ne sont placés sur la cible que d'après leur note, et
          cette game n'en a pas (résultat saisi à la main, ou stats absentes).
        </AlertDescription>
      </Alert>
    );
  }

  return (
    <section role="group" aria-label="Cibles" className="flex flex-col gap-3">
      <div className="relative mx-auto aspect-square w-full max-w-2xl">
        {ANNEAUX_TRACES.map((classe) => (
          <div key={classe} aria-hidden className={cn("absolute rounded-full border border-dashed border-border", classe)} />
        ))}
        {places.map((p) => (
          <Pastille key={p.joueur.playerId} place={p} />
        ))}
      </div>
      <p className="flex items-center justify-center gap-4 text-xs text-muted-foreground">
        <span className="flex items-center gap-1.5">
          <span className={cn("size-3 rounded-full border-2", sideBorderClass("blue"))} /> Équipe bleue
        </span>
        <span className="flex items-center gap-1.5">
          <span className={cn("size-3 rounded-full border-2", sideBorderClass("red"))} /> Équipe rouge
        </span>
      </p>
    </section>
  );
}

function Pastille({ place }: { place: Place }) {
  const { joueur, side, anneau, classe } = place;
  const centre = anneau === 0;

  return (
    <div data-anneau={anneau} className={cn("absolute -translate-x-1/2 -translate-y-1/2", classe)}>
      <Link
        to="/joueurs/$playerId"
        params={{ playerId: String(joueur.playerId) }}
        data-equipe={side}
        className="group flex w-20 flex-col items-center gap-0.5 text-center sm:w-24"
      >
        <AvatarJoueur
          prenom={joueur.prenom}
          avatarUrl={joueur.avatarUrl}
          className={cn(
            "rounded-full border-[3px] transition-transform group-hover:scale-105",
            sideBorderClass(side),
            centre ? "size-16 text-xl sm:size-24 sm:text-3xl" : "size-11 text-sm sm:size-16 sm:text-lg",
          )}
        />
        <span className="max-w-full truncate text-xs font-medium sm:text-sm">{joueur.prenom}</span>
        {joueur.performance && (
          <span className="font-mono text-xs font-semibold tabular-nums sm:text-sm">{joueur.performance.note}</span>
        )}
        {joueur.badge === "MeilleurePerf" && (
          <span className="rounded bg-emerald-500 px-1 text-[10px] leading-4 font-semibold whitespace-nowrap text-black">
            Meilleure perf
          </span>
        )}
        {joueur.badge === "Ace" && (
          <span className="rounded bg-fuchsia-500 px-1 text-[10px] leading-4 font-semibold text-black">ACE</span>
        )}
      </Link>
    </div>
  );
}
