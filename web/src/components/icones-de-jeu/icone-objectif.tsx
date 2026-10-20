import { useState } from "react";
import { cn } from "@/lib/utils";

export type Objectif = "tourelle" | "dragon" | "baron" | "heraut" | "larve" | "inhibiteur";
export type EquipeObjectif = 100 | 200;

/** Les icônes d'objectifs officielles de Riot, servies par Community Dragon (100 = équipe bleue, 200 = rouge). */
export const BASE_ICONES_D_OBJECTIFS =
  "https://raw.communitydragon.org/latest/plugins/rcp-fe-lol-match-history/global/default";

const NOMS: Record<Objectif, string> = {
  tourelle: "Tourelle",
  dragon: "Dragon",
  baron: "Baron",
  heraut: "Héraut",
  larve: "Larve du Néant",
  inhibiteur: "Inhibiteur",
};

/** Le nom de fichier par équipe chez Riot (<nom>-<100|200>.png) ; la larve n'a pas d'icône par équipe. */
const FICHIERS: Record<Exclude<Objectif, "larve">, string> = {
  tourelle: "tower",
  dragon: "dragon",
  baron: "baron",
  heraut: "herald",
  inhibiteur: "inhibitor",
};

/** La larve du Néant n'existe que dans une planche de 7 icônes de 80 px, empilées ; la sienne est la sixième. */
const PLANCHE_LARVE = { url: `${BASE_ICONES_D_OBJECTIFS}/right_icons_grub.png`, largeur: 80, hauteur: 560, y: 400 };

const urlParEquipe = (objectif: Exclude<Objectif, "larve">, equipe: EquipeObjectif) =>
  `${BASE_ICONES_D_OBJECTIFS}/${FICHIERS[objectif]}-${equipe}.png`;

/** L'icône d'un objectif d'équipe dans le HTML ; son nom accessible est celui de l'objectif. Sans équipe : version bleue. Image indisponible : pastille neutre. */
export function IconeObjectif({
  objectif,
  equipe = 100,
  className,
}: {
  objectif: Objectif;
  equipe?: EquipeObjectif;
  className?: string;
}) {
  const [enEchec, setEnEchec] = useState(false);
  const classes = cn("inline-block size-5 shrink-0", className);
  if (enEchec) {
    return <span role="img" aria-label={NOMS[objectif]} className={cn(classes, "rounded-full bg-muted-foreground/30")} />;
  }
  if (objectif === "larve") {
    return (
      <span role="img" aria-label={NOMS[objectif]} className={cn(classes, "relative overflow-hidden")}>
        <img
          src={PLANCHE_LARVE.url}
          alt=""
          onError={() => setEnEchec(true)}
          className="absolute left-0 top-0 h-[700%] w-full max-w-none -translate-y-[71.43%] object-fill"
        />
      </span>
    );
  }
  return (
    <img src={urlParEquipe(objectif, equipe)} alt={NOMS[objectif]} onError={() => setEnEchec(true)} className={classes} />
  );
}

/** L'icône d'un objectif à l'intérieur d'un SVG (une `<img>` n'y est pas rendue), centrée en (cx, cy) sur `taille` px. */
export function IconeObjectifSvg({
  objectif,
  equipe = 100,
  cx,
  cy,
  taille,
}: {
  objectif: Objectif;
  equipe?: EquipeObjectif;
  cx: number;
  cy: number;
  taille: number;
}) {
  const [enEchec, setEnEchec] = useState(false);
  if (enEchec) return <circle cx={cx} cy={cy} r={taille / 4} className="fill-muted-foreground/40" />;
  const x = cx - taille / 2;
  const y = cy - taille / 2;
  if (objectif === "larve") {
    return (
      <svg x={x} y={y} width={taille} height={taille} viewBox={`0 ${PLANCHE_LARVE.y} 80 80`}>
        <image
          href={PLANCHE_LARVE.url}
          width={PLANCHE_LARVE.largeur}
          height={PLANCHE_LARVE.hauteur}
          onError={() => setEnEchec(true)}
        />
      </svg>
    );
  }
  return (
    <image href={urlParEquipe(objectif, equipe)} x={x} y={y} width={taille} height={taille} onError={() => setEnEchec(true)} />
  );
}
