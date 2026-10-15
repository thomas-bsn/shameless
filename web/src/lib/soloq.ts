import { isMasterPlus, normalizeTier, SOLOQ_TIERS, type SoloQTier } from "./ranks";

/** Ordre croissant des tiers classés, de Iron à Challenger. */
const ASCENDING = SOLOQ_TIERS.filter((t) => t !== "UNRANKED").reverse();

export interface Rank {
  tier: SoloQTier;
  division: number | null;
  lp: number;
}

interface RankedPlayer {
  rankTier: string | null;
  rankDivision: number | null;
  lp: number | null;
  peakTier: string | null;
  peakDivision: number | null;
  peakLp: number;
}

/** Score linéaire d'un rang : 400 par tier, 100 par division, plus les LP. */
export function rankScore(tier: string, division: number | null, lp: number | null): number {
  const normalized = normalizeTier(tier);
  const tierScore = Math.max(0, ASCENDING.indexOf(normalized as (typeof ASCENDING)[number])) * 400;
  const divisionScore = isMasterPlus(normalized) ? 0 : (4 - (division ?? 4)) * 100;
  return tierScore + divisionScore + (lp ?? 0);
}

export function scoreToRank(score: number): Rank {
  const index = Math.max(0, Math.min(Math.floor(score / 400), ASCENDING.length - 1));
  const tier = ASCENDING[index]!;
  const remainder = Math.max(0, score - index * 400);
  if (isMasterPlus(tier)) return { tier, division: null, lp: Math.round(remainder) };
  return { tier, division: 4 - Math.min(Math.floor(remainder / 100), 3), lp: Math.round(remainder % 100) };
}

/** Le rang affiché au classement : le peak s'il dépasse le rang actuel. */
export function displayRank(player: RankedPlayer) {
  const current = {
    tier: normalizeTier(player.rankTier),
    division: player.rankDivision,
    lp: player.lp,
    isPeak: false,
  };
  const peak = player.peakTier
    ? { tier: normalizeTier(player.peakTier), division: player.peakDivision, lp: player.peakLp, isPeak: true }
    : null;

  if (!peak || peak.tier === "UNRANKED") {
    return current.tier === "UNRANKED" ? { ...current, division: null, lp: null } : current;
  }
  if (current.tier === "UNRANKED") return peak;
  return rankScore(peak.tier, peak.division, peak.lp) > rankScore(current.tier, current.division, current.lp)
    ? peak
    : current;
}

/** Les tiers non vides, de Challenger à Unranked ; dans un tier, le meilleur rang d'abord. */
export function groupBySoloQTier<P extends RankedPlayer>(players: P[]) {
  const ranked = players
    .map((player) => ({ player, rank: displayRank(player) }))
    .sort((a, b) => rankScore(b.rank.tier, b.rank.division, b.rank.lp) - rankScore(a.rank.tier, a.rank.division, a.rank.lp));

  return SOLOQ_TIERS.map((tier) => ({
    tier,
    players: ranked.filter((r) => r.rank.tier === tier).map((r) => r.player),
  })).filter((group) => group.players.length > 0);
}

/** Niveau du groupe : rangs médian et moyen des Joueurs classés. */
export function soloQLevel(players: RankedPlayer[]): { median: Rank; average: Rank } | null {
  const scores = players
    .map(displayRank)
    .filter((r) => r.tier !== "UNRANKED")
    .map((r) => rankScore(r.tier, r.division, r.lp))
    .sort((a, b) => a - b);
  if (scores.length === 0) return null;

  const middle = Math.floor(scores.length / 2);
  const median = scores.length % 2 === 1 ? scores[middle]! : (scores[middle - 1]! + scores[middle]!) / 2;
  const average = scores.reduce((sum, s) => sum + s, 0) / scores.length;
  return { median: scoreToRank(median), average: scoreToRank(average) };
}

export function averageCost(players: { currentCost: number }[]): number | null {
  if (players.length === 0) return null;
  return players.reduce((sum, p) => sum + p.currentCost, 0) / players.length;
}
