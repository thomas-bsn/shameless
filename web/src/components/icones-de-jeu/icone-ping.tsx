import type { Schemas } from "@/api/client";
import { useState } from "react";
import { cn } from "@/lib/utils";

export type Ping = keyof NonNullable<Schemas["PingsDuJoueur"]>;

const NOMS: Record<Ping, string> = {
  allIn: "Tous en avant",
  assistMe: "À l'aide",
  basic: "Basique",
  command: "Commande",
  danger: "Danger",
  enemyMissing: "Ennemi disparu",
  enemyVision: "Ennemi en vue",
  getBack: "Repli",
  hold: "Tenir la position",
  needVision: "Besoin de vision",
  onMyWay: "En route",
  push: "Pousser",
  retreat: "Retraite",
  visionCleared: "Zone dégagée",
};

const FLECHE_HAUT = "M12 3l7 9h-4v9h-6v-9H5z";
const FLECHE_BAS = "M12 21l-7-9h4V3h6v9h4z";
const OEIL = "M12 5C6.5 5 3 12 3 12s3.5 7 9 7 9-7 9-7-3.5-7-9-7zm0 3.5a3.5 3.5 0 1 1 0 7 3.5 3.5 0 0 1 0-7z";

const TRACES: Record<Ping, string> = {
  allIn: FLECHE_HAUT,
  assistMe: "M12 3l9 17H3zm-1 6v5h2V9zm1 7a1.2 1.2 0 1 0 0 2.4 1.2 1.2 0 0 0 0-2.4z",
  basic: "M12 2a7 7 0 0 1 7 7c0 5-7 13-7 13S5 14 5 9a7 7 0 0 1 7-7z",
  command: "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zm0 4a5 5 0 1 1 0 10 5 5 0 0 1 0-10zm0 3a2 2 0 1 0 0 4 2 2 0 0 0 0-4z",
  danger: "M10.5 3h3l-.6 12h-1.8zM12 17.5a1.7 1.7 0 1 1 0 3.4 1.7 1.7 0 0 1 0-3.4z",
  enemyMissing: "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zm-1 13v-2h2v2zm0-4V7h2v5z",
  enemyVision: OEIL,
  getBack: FLECHE_BAS,
  hold: "M5 5h14v14H5z",
  needVision: OEIL,
  onMyWay: "M4 12l8-8v5h8v6h-8v5z",
  push: FLECHE_HAUT,
  retreat: FLECHE_BAS,
  visionCleared: "M12 3a9 9 0 1 0 0 18 9 9 0 0 0 0-18zm-1.5 12.5L6.5 11.5l1.4-1.4 2.6 2.6 5.6-5.6 1.4 1.4z",
};

/** Les icônes de ping officielles de Riot, servies par Community Dragon. */
const BASE_ICONES_DE_PING = "https://raw.communitydragon.org/latest/game/assets/ux/minimap/pings";

/** Le fichier officiel de chaque ping ; « command » n'a pas d'équivalent sûr et garde son tracé. */
const FICHIERS: Partial<Record<Ping, string>> = {
  allIn: "all_in",
  assistMe: "assist",
  basic: "ping",
  danger: "caution",
  enemyMissing: "mia_new",
  enemyVision: "enemychampsighted",
  getBack: "retreat",
  hold: "hold",
  needVision: "need_ward",
  onMyWay: "on_my_way_new",
  push: "push",
  retreat: "retreat",
  visionCleared: "cleared",
};

/** L'icône d'un ping ; son nom accessible est son nom français. Icône officielle quand elle existe, sinon (ou si elle ne charge pas) un tracé. */
export function IconePing({ ping, className }: { ping: Ping; className?: string }) {
  const [enEchec, setEnEchec] = useState(false);
  const fichier = FICHIERS[ping];
  if (fichier && !enEchec) {
    return (
      <img
        src={`${BASE_ICONES_DE_PING}/${fichier}.png`}
        alt={NOMS[ping]}
        onError={() => setEnEchec(true)}
        className={cn("inline-block size-5 shrink-0", className)}
      />
    );
  }
  return (
    <svg
      role="img"
      aria-label={NOMS[ping]}
      viewBox="0 0 24 24"
      className={cn("inline-block size-5 shrink-0 text-muted-foreground", className)}
    >
      <path d={TRACES[ping]} fill="currentColor" fillRule="evenodd" />
    </svg>
  );
}
