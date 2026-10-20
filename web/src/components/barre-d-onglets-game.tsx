import { useEffect, useRef, useState, type ComponentType, type SVGProps } from "react";
import { TabsList, TabsTrigger } from "@/components/ui/tabs";
import { cn } from "@/lib/utils";

type Icone = ComponentType<SVGProps<SVGSVGElement>>;

function Svg(props: SVGProps<SVGSVGElement>) {
  return (
    <svg
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={2}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      {...props}
    />
  );
}

/** Général : le tableau des joueurs. */
const IconeGeneral: Icone = (props) => (
  <Svg {...props}>
    <rect x="3" y="4" width="18" height="16" rx="2" />
    <path d="M3 10h18M3 15h18M9 4v16" />
  </Svg>
);

/** Performance : la cible et son centre. */
const IconePerformance: Icone = (props) => (
  <Svg {...props}>
    <circle cx="12" cy="12" r="9" />
    <circle cx="12" cy="12" r="5" />
    <circle cx="12" cy="12" r="1" fill="currentColor" />
  </Svg>
);

/** Courbes : une courbe qui monte, sur ses axes. */
const IconeCourbes: Icone = (props) => (
  <Svg {...props}>
    <path d="M3 3v18h18" />
    <path d="m7 15 4-5 3 3 6-7" />
  </Svg>
);

/** Détails : des barres de statistiques. */
const IconeDetails: Icone = (props) => (
  <Svg {...props}>
    <path d="M4 20V10M10 20V4M16 20v-7M22 20H2" />
  </Svg>
);

/** Runes : un cercle et son étoile. */
const IconeRunes: Icone = (props) => (
  <Svg {...props}>
    <circle cx="12" cy="12" r="9" />
    <path d="m12 6 2 4.5 4.5.5-3.4 3 1 4.5L12 16l-4.1 2.5 1-4.5-3.4-3 4.5-.5z" />
  </Svg>
);

/** Replay 3D : un cube. */
const IconeReplay3D: Icone = (props) => (
  <Svg {...props}>
    <path d="M12 3 4 7.5v9L12 21l8-4.5v-9z" />
    <path d="m4 7.5 8 4.5 8-4.5M12 12v9" />
  </Svg>
);

const ONGLETS: { valeur: string; nom: string; Icone: Icone; grise?: boolean }[] = [
  { valeur: "general", nom: "Général", Icone: IconeGeneral },
  { valeur: "cibles", nom: "Performance", Icone: IconePerformance },
  { valeur: "courbes", nom: "Courbes", Icone: IconeCourbes },
  { valeur: "details", nom: "Détails", Icone: IconeDetails },
  { valeur: "runes", nom: "Runes", Icone: IconeRunes },
  { valeur: "replay", nom: "Replay 3D", Icone: IconeReplay3D, grise: true },
];

/**
 * La barre d'onglets de la page d'une game : pleine largeur, collante en haut de la page,
 * avec un fond plus opaque et une ombre une fois collée. À mettre dans un composant Tabs.
 */
export function BarreDOngletsGame() {
  const sentinelle = useRef<HTMLDivElement>(null);
  const [collee, setCollee] = useState(false);

  useEffect(() => {
    const el = sentinelle.current;
    if (!el || typeof IntersectionObserver === "undefined") return;
    const observer = new IntersectionObserver(([entree]) => setCollee(!entree.isIntersecting));
    observer.observe(el);
    return () => observer.disconnect();
  }, []);

  return (
    <>
      {/* Repère juste au-dessus de la barre : quand il sort de l'écran, la barre est collée. */}
      <div ref={sentinelle} aria-hidden="true" className="-mb-2 h-px" />
      <TabsList
        variant="line"
        data-colle={collee}
        className={cn(
          "sticky top-0 z-20 h-auto w-full justify-stretch gap-0 rounded-none border-b bg-background/80 p-0 backdrop-blur transition-shadow",
          collee && "bg-background/95 shadow-md",
        )}
      >
        {ONGLETS.map(({ valeur, nom, Icone, grise }) => (
          <TabsTrigger
            key={valeur}
            value={valeur}
            className={cn(
              "h-auto flex-1 flex-col gap-1 rounded-none px-1 py-2 text-xs after:bottom-0! after:h-0.5! sm:flex-row sm:gap-2 sm:px-3 sm:py-3 sm:text-sm",
              "data-[state=active]:bg-muted/60",
              grise && "opacity-60",
            )}
          >
            <Icone className="size-5 sm:size-4" />
            {nom}
          </TabsTrigger>
        ))}
      </TabsList>
    </>
  );
}
