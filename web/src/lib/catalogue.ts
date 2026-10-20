import { $api } from "@/api/client";

export interface ElementDuCatalogue {
  id: number;
  name: string;
  iconUrl: string;
}

/**
 * Le catalogue Data Dragon (objets, sorts, runes) ; il peut être indisponible (503).
 * Dans ce cas, chaque recherche répond « inconnu » et l'appelant affiche l'id.
 */
export function useCatalogue() {
  const requete = $api.useQuery("get", "/catalogue", {}, { staleTime: Infinity, retry: false });
  const catalogue = requete.data;
  const arbres = catalogue?.arbres ?? [];
  const trouve = (liste: ElementDuCatalogue[] | undefined, id: number): ElementDuCatalogue | undefined =>
    liste?.find((e) => e.id === id);

  return {
    objet: (id: number) => trouve(catalogue?.objets, id),
    sort: (id: number) => trouve(catalogue?.sorts, id),
    arbre: (id: number) => trouve(arbres, id),
    /** La capacité Q, W, E ou R d'un champion (id Data Dragon, « LeeSin »). */
    capacite: (championId: string, touche: string) =>
      catalogue?.champions.find((c) => c.id === championId)?.capacites.find((c) => c.touche === touche),
    rune: (id: number) =>
      trouve(
        arbres.flatMap((a) => a.runes),
        id,
      ),
  };
}
