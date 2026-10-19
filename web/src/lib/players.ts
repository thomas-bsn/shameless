import { queryOptions } from "@tanstack/react-query";
import { fetchClient, type Schemas } from "@/api/client";
import { normalizeTier, roleLabel } from "./ranks";
import { compareRanks } from "./soloq";

type RankKey = Parameters<typeof compareRanks>[0];

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

/** Recherche par prénom, RiotId ou rôle (« adc », « jungle »). */
export function matchesPlayerOrRole(player: { prenom: string; riotId: string; mainRole: string | null }, search: string): boolean {
  const needle = fold(search.trim());
  return matchesPlayer(player, search) || fold(roleLabel(player.mainRole) ?? "").includes(needle);
}

export type PlayerSortKey = "joueur" | "cost" | "role" | "rang" | "peak" | "winrate";
export type SortDirection = "asc" | "desc";

/**
 * Trie les Joueurs du tableau. Les valeurs absentes (peak non renseigné, aucune game en customs)
 * restent en bas quel que soit le sens.
 */
export function sortPlayers(
  players: Schemas["PlayerDto"][],
  key: PlayerSortKey,
  direction: SortDirection,
  winrates: ReadonlyMap<number, number>,
): Schemas["PlayerDto"][] {
  const value = (p: Schemas["PlayerDto"]): number | string | RankKey | null => {
    switch (key) {
      case "joueur":
        return fold(p.prenom);
      case "cost":
        return p.currentCost;
      case "role":
        return roleLabel(p.mainRole);
      case "rang":
        return { tier: normalizeTier(p.rankTier), division: p.rankDivision, lp: p.lp };
      case "peak":
        return p.peakTier ? { tier: normalizeTier(p.peakTier), division: p.peakDivision, lp: p.peakLp } : null;
      case "winrate":
        return winrates.get(p.id) ?? null;
    }
  };
  const sign = direction === "asc" ? 1 : -1;
  return [...players].sort((a, b) => {
    const va = value(a);
    const vb = value(b);
    if (va === null || vb === null) return va === vb ? 0 : va === null ? 1 : -1;
    const order =
      typeof va === "string"
        ? va.localeCompare(vb as string, "fr")
        : typeof va === "number"
          ? va - (vb as number)
          : compareRanks(va, vb as RankKey);
    return sign * order || a.prenom.localeCompare(b.prenom, "fr");
  });
}
