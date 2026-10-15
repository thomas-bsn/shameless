import { queryOptions } from "@tanstack/react-query";
import { fetchClient } from "@/api/client";

/** Le Joueur lié au compte Discord connecté, ou null s'il n'y en a pas. */
export const myPlayerQuery = queryOptions({
  queryKey: ["players", "me"],
  queryFn: async () => {
    const { data, error, response } = await fetchClient.GET("/players/me");
    if (response.status === 404) return null;
    if (error) throw error;
    return data;
  },
});

const fold = (text: string) =>
  text
    .normalize("NFD")
    .replace(/[̀-ͯ]/g, "")
    .toLowerCase();

/** Recherche par prénom ou RiotId, sans tenir compte de la casse ni des accents. */
export function matchesPlayer(player: { prenom: string; riotId: string }, search: string): boolean {
  const needle = fold(search.trim());
  return fold(player.prenom).includes(needle) || fold(player.riotId).includes(needle);
}
