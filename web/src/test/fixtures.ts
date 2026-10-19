import type { Schemas } from "@/api/client";

export const user = (overrides: Partial<Schemas["AuthUserResponse"]> = {}): Schemas["AuthUserResponse"] => ({
  valid: true,
  discordUserId: "1001",
  username: "Thomas",
  avatarUrl: "https://cdn.discordapp.com/embed/avatars/1.png",
  isAdmin: false,
  role: "Utilisateur",
  bannerUrl: null,
  bannerColor: null,
  ...overrides,
});

export const player = (overrides: Partial<Schemas["PlayerDto"]> = {}): Schemas["PlayerDto"] => ({
  id: 1,
  prenom: "Alice",
  riotId: "Alice#EUW",
  puuid: "puuid-alice",
  rankTier: "GOLD",
  rankDivision: 2,
  mainRole: "MIDDLE",
  lp: 40,
  flexRankTier: null,
  flexRankDivision: null,
  flexLp: null,
  peakTier: null,
  peakDivision: null,
  peakSeason: null,
  peakLp: 0,
  currentCost: 3,
  costEvolution: null,
  avatarUrl: null,
  ...overrides,
});
