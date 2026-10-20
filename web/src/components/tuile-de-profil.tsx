import type { ReactNode } from "react";
import { cn } from "@/lib/utils";

/** Une tuile : icône colorée, libellé, valeur et détail optionnel. Le groupe porte le libellé comme nom. */
export function TuileDeProfil({
  libelle,
  valeur,
  detail,
  icone,
  couleur,
}: {
  libelle: string;
  valeur?: string;
  detail?: string;
  icone: ReactNode;
  couleur: string;
}) {
  return (
    <div role="group" aria-label={libelle} className="flex flex-col items-center gap-1 rounded-xl border bg-background/40 px-2 py-3 text-center">
      <span className={cn("flex size-9 items-center justify-center", couleur)}>{icone}</span>
      <span className="text-xs text-muted-foreground">{libelle}</span>
      {valeur && <span className={cn("text-base font-bold tabular-nums", couleur)}>{valeur}</span>}
      {detail && <span className="text-xs text-muted-foreground">{detail}</span>}
    </div>
  );
}

/** La pièce du cost. */
export function IconePiece({ className }: { className?: string }) {
  return (
    <svg viewBox="0 0 24 24" aria-hidden className={cn("size-8", className)}>
      <circle cx="12" cy="12" r="9" fill="currentColor" fillOpacity="0.2" stroke="currentColor" strokeWidth="1.8" />
      <path
        d="M14.5 9.2c-.5-.8-1.4-1.2-2.5-1.2-1.4 0-2.5.8-2.5 2 0 3 5 1.4 5 4 0 1.2-1.1 2-2.5 2-1.1 0-2-.4-2.5-1.2M12 6.5V8m0 8v1.5"
        fill="none"
        stroke="currentColor"
        strokeWidth="1.6"
        strokeLinecap="round"
      />
    </svg>
  );
}
