import type { Schemas } from "@/api/client";
import { outcomeLabel, pendingGame, replacementCandidates } from "@/lib/series";

const game = (id: number, status: string, winner: string | null): Schemas["SeriesGameResponse"] => ({
  id,
  status,
  teamASide: "blue",
  teamBSide: "red",
  winner,
  teamA: [],
  teamB: [],
  riotMatchId: null,
  championsJoues: [],
  rapportEnRevue: null,
});

const participant = (playerId: number): Schemas["GameParticipantDto"] => ({
  prenom: `J${playerId}`,
  riotId: `J${playerId}#EUW`,
  rankTier: null,
  rankDivision: null,
  cost: null,
  playerId,
  role: null,
});

describe("outcomeLabel", () => {
  it("nomme l'Équipe gagnante d'une Série gagnée", () => {
    expect(outcomeLabel({ outcome: "Gagnee", winningTeam: "B" })).toBe("Gagnée par l'Équipe B");
  });

  it("décrit les autres issues", () => {
    expect(outcomeLabel({ outcome: "EnCours", winningTeam: null })).toBe("En cours");
    expect(outcomeLabel({ outcome: "Nulle", winningTeam: null })).toBe("Nulle");
    expect(outcomeLabel({ outcome: "Interrompue", winningTeam: null })).toBe("Interrompue");
  });
});

describe("pendingGame", () => {
  it("renvoie la game qui attend son résultat", () => {
    expect(pendingGame({ games: [game(1, "completed", "A"), game(2, "pending", null)] })?.id).toBe(2);
  });

  it("renvoie null entre deux games", () => {
    expect(pendingGame({ games: [game(1, "completed", "A")] })).toBeNull();
    expect(pendingGame({ games: [] })).toBeNull();
  });
});

describe("replacementCandidates", () => {
  it("exclut les dix joueurs de la composition courante", () => {
    const players = [1, 2, 11, 12].map((id) => ({ id, prenom: `J${id}` }));
    const series = { teamA: [participant(1)], teamB: [participant(2)] };

    expect(replacementCandidates(players, series).map((p) => p.id)).toEqual([11, 12]);
  });
});
