import { useState } from "react";
import { cn } from "@/lib/utils";
import { EmplacementNeutre } from "./emplacement-neutre";

export type Role = "Top" | "Jungle" | "Mid" | "Adc" | "Support";

/** Les icônes de position officielles de Riot, servies par Community Dragon. */
const BASE_ICONES_DE_POSITION =
  "https://raw.communitydragon.org/latest/plugins/rcp-fe-lol-static-assets/global/default/svg";

const NOMS: Record<Role, string> = { Top: "Top", Jungle: "Jungle", Mid: "Mid", Adc: "ADC", Support: "Support" };

/** Le nom de fichier de la position chez Riot (position-<nom>.svg). */
const POSITIONS: Record<Role, string> = {
  Top: "top",
  Jungle: "jungle",
  Mid: "middle",
  Adc: "bottom",
  Support: "utility",
};

/** L'icône officielle d'un des cinq rôles ; son nom accessible est celui du rôle (Top, Jungle, Mid, ADC, Support). Rôle inconnu ou image indisponible : emplacement neutre. */
export function IconeRole({ role, className }: { role: Role; className?: string }) {
  const [enEchec, setEnEchec] = useState(false);
  const position = POSITIONS[role];
  if (!position || enEchec) return <EmplacementNeutre className={cn("size-5", className)} />;
  return (
    <img
      src={`${BASE_ICONES_DE_POSITION}/position-${position}.svg`}
      alt={NOMS[role]}
      onError={() => setEnEchec(true)}
      className={cn("inline-block size-5 shrink-0", className)}
    />
  );
}
