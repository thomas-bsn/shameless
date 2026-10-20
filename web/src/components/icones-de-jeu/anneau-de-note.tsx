import { palierDeNote, type PalierDeNote } from "@/lib/note";
import { cn } from "@/lib/utils";

const COULEURS: Record<PalierDeNote, string> = {
  or: "stroke-amber-400 drop-shadow-[0_0_6px_rgb(251_191_36/0.8)]",
  vert: "stroke-emerald-500",
  bleu: "stroke-sky-500",
  orange: "stroke-orange-500",
  rouge: "stroke-red-500",
};

const RAYON = 15;
const CIRCONFERENCE = 2 * Math.PI * RAYON;

/** La note sur 100 d'un joueur dans un anneau coloré selon son palier ; le chiffre garde la couleur du texte. */
export function AnneauDeNote({
  note,
  className,
  sansValeur = false,
}: {
  note: number;
  className?: string;
  /** Anneau vide, quand la note est déjà écrite à côté. */
  sansValeur?: boolean;
}) {
  const palier = palierDeNote(note);
  const arrondie = Math.round(note);
  const part = Math.min(Math.max(note, 0), 100) / 100;
  return (
    <span
      data-palier={palier}
      role="img"
      aria-label={`Note ${arrondie} sur 100`}
      className={cn("relative inline-flex size-9 shrink-0 items-center justify-center", className)}
    >
      <svg viewBox="0 0 36 36" className={cn("absolute inset-0 size-full -rotate-90", COULEURS[palier])} aria-hidden>
        <circle cx="18" cy="18" r={RAYON} fill="none" strokeWidth="3" className="stroke-muted" />
        <circle
          cx="18"
          cy="18"
          r={RAYON}
          fill="none"
          strokeWidth="3"
          strokeLinecap="round"
          strokeDasharray={`${part * CIRCONFERENCE} ${CIRCONFERENCE}`}
        />
      </svg>
      {!sansValeur && (
        <span aria-hidden className="relative font-mono text-xs font-semibold tabular-nums text-foreground">
          {arrondie}
        </span>
      )}
    </span>
  );
}
