import { cn } from "@/lib/utils";

export type Titre = "MVP" | "ACE" | "DOFD";

const DESCRIPTIONS: Record<Titre, string> = {
  MVP: "Meilleure note de l'équipe gagnante",
  ACE: "Meilleure note de l'équipe perdante",
  DOFD: "Dick of the Day, élu par vote",
};

const COULEURS: Record<Titre, string> = {
  MVP: "bg-amber-400/15 text-amber-500 ring-amber-400/40",
  ACE: "bg-sky-500/15 text-sky-500 ring-sky-500/40",
  DOFD: "bg-orange-900/25 text-orange-400 ring-orange-700/50",
};

/** Le badge MVP (meilleure note de l'équipe gagnante), ACE (équipe perdante) ou DOFD (votes clos). */
export function BadgeDeTitre({ titre, className }: { titre: Titre; className?: string }) {
  return (
    <span
      title={DESCRIPTIONS[titre]}
      className={cn(
        "inline-flex h-4 items-center rounded px-1.5 text-[10px] font-bold tracking-wide ring-1 ring-inset",
        COULEURS[titre],
        className,
      )}
    >
      {titre}
    </span>
  );
}
