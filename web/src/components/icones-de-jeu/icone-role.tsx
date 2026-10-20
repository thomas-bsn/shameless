import { cn } from "@/lib/utils";

export type Role = "Top" | "Jungle" | "Mid" | "Adc" | "Support";

const NOMS: Record<Role, string> = { Top: "Top", Jungle: "Jungle", Mid: "Mid", Adc: "ADC", Support: "Support" };

/** Le fond commun : la carte en trait léger, avec sa diagonale. */
const FOND = (
  <g fill="none" stroke="currentColor" strokeWidth="1.5" opacity="0.4">
    <path d="M4 20V4h16" />
    <path d="M4 20 20 4" />
    <path d="M20 4v16H4" />
  </g>
);

/** Dans chaque pictogramme, la voie occupée est pleine. */
const VOIES: Record<Role, string> = {
  Top: "M4 4h12l-4 4v4H8v4l-4 4z",
  Jungle: "M12 3c1 4 5 6 5 11a5 5 0 0 1-10 0c0-5 4-7 5-11zm-1 9v9h2v-9z",
  Mid: "M9 4l11 11v5h-5L4 9V4z",
  Adc: "M20 20H8l4-4h4v-4l4-4z",
  Support: "M12 3l2.6 5.4 5.9.9-4.3 4.2 1 5.9L12 16.5 6.8 19.4l1-5.9-4.3-4.2 5.9-.9z",
};

/** L'icône d'un des cinq rôles ; son nom accessible est celui du rôle (Top, Jungle, Mid, ADC, Support). */
export function IconeRole({ role, className }: { role: Role; className?: string }) {
  return (
    <svg
      role="img"
      aria-label={NOMS[role]}
      viewBox="0 0 24 24"
      className={cn("inline-block size-5 shrink-0 text-muted-foreground", className)}
    >
      {role !== "Support" && FOND}
      <path d={VOIES[role]} fill="currentColor" />
    </svg>
  );
}
