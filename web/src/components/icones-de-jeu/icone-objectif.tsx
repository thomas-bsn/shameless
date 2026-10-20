import { cn } from "@/lib/utils";

export type Objectif = "tourelle" | "dragon" | "baron" | "heraut" | "larve" | "inhibiteur";

const NOMS: Record<Objectif, string> = {
  tourelle: "Tourelle",
  dragon: "Dragon",
  baron: "Baron",
  heraut: "Héraut",
  larve: "Larve du Néant",
  inhibiteur: "Inhibiteur",
};

const TRACES: Record<Objectif, string> = {
  tourelle: "M6 3h3v2h2V3h2v2h2V3h3v6l-2 2v9H8v-9L6 9z",
  dragon: "M3 5l5 2 4-3 4 3 5-2-2 7-3 3v5l-4-2-4 2v-5l-3-3z",
  baron: "M4 4c3 0 5 2 8 2s5-2 8-2c0 6-2 10-5 12l-3 4-3-4C6 14 4 10 4 4z",
  heraut: "M12 3l8 5v5l-4 3v5l-4-2-4 2v-5l-4-3V8zM9 10h2v2H9zm4 0h2v2h-2z",
  larve: "M12 3c4 0 7 3 7 6 0 2-1 3-1 5s2 3 2 5c-3 0-4-2-8-2s-5 2-8 2c0-2 2-3 2-5s-1-3-1-5c0-3 3-6 7-6z",
  inhibiteur: "M12 2l6 8-6 12-6-12zM12 7l-2.5 3.2L12 15l2.5-4.8z",
};

/** L'icône d'un objectif d'équipe ; son nom accessible est celui de l'objectif. */
export function IconeObjectif({ objectif, className }: { objectif: Objectif; className?: string }) {
  return (
    <svg
      role="img"
      aria-label={NOMS[objectif]}
      viewBox="0 0 24 24"
      className={cn("inline-block size-5 shrink-0 text-muted-foreground", className)}
    >
      <path d={TRACES[objectif]} fill="currentColor" fillRule="evenodd" />
    </svg>
  );
}
