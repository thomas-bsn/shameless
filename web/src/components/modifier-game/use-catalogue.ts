import { useMemo } from "react";
import { $api, type Schemas } from "@/api/client";
import type { TypeDeChamp } from "@/lib/champs-corrigeables";

export type EntreeDuCatalogue = { id: number; name: string; iconUrl: string };

/** Les choix d'un type de champ (objets, sorts, runes, arbres) tels que Data Dragon les nomme. */
export type Catalogue = Record<"objet" | "sort" | "rune" | "arbre", EntreeDuCatalogue[]>;

const enListes = (c: Schemas["CatalogueDeJeu"]): Catalogue => ({
  objet: c.objets,
  sort: c.sorts,
  rune: c.arbres.flatMap((a) => a.runes),
  arbre: c.arbres,
});

/**
 * Le catalogue de jeu (noms et icônes). Indisponible (503) ou en chargement : null, et les cases
 * retombent sur des champs d'identifiants numériques.
 */
export function useCatalogue(): Catalogue | null {
  const catalogue = $api.useQuery("get", "/catalogue", undefined, { staleTime: Infinity, retry: 1 });
  return useMemo(() => (catalogue.data ? enListes(catalogue.data) : null), [catalogue.data]);
}

export const entreesDe = (catalogue: Catalogue | null, type: TypeDeChamp): EntreeDuCatalogue[] | null =>
  catalogue && (type === "objet" || type === "sort" || type === "rune" || type === "arbre") ? catalogue[type] : null;
