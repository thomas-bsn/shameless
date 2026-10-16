import { $api } from "@/api/client";

/** Rafraîchissement d'une Draft de champions en cours (polling), pour les capitaines, le spectateur et l'organisateur. */
export const POLLING_DRAFT_MS = 1500;

/** Le catalogue des champions (Data Dragon) : statique, chargé une fois. */
export function useChampions() {
  return $api.useQuery("get", "/champions", {}, { staleTime: Infinity });
}
