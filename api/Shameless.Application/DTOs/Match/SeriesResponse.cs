using Shameless.Application.Entities;
using Shameless.Application.Stats;

namespace Shameless.Application.DTOs.Match;

/// <summary>
/// Une Série, son issue et ses deux Équipes. La composition d'une Équipe est sa composition
/// courante (celle de la prochaine game) ; chaque game donne le Side de chaque Équipe et sa
/// composition pour cette game.
/// </summary>
public record SeriesResponse(
    int SeriesId,
    int? SoireeId,
    int Format,
    bool Fearless,
    SeriesOutcome Outcome,
    string? WinningTeam, // "A" | "B" quand la Série est gagnée
    int TeamAWins,
    int TeamBWins,
    List<GameParticipantDto> TeamA,
    List<GameParticipantDto> TeamB,
    int? CapitaineA, // le Capitaine de l'Équipe A, null tant qu'aucun n'est désigné
    int? CapitaineB,
    List<SeriesGameResponse> Games
);

public record SeriesGameResponse(
    int Id,
    string Status,
    string TeamASide,
    string TeamBSide,
    string? Winner,
    List<GameParticipantDto> TeamA,
    List<GameParticipantDto> TeamB,
    string? RiotMatchId, // l'identifiant Riot de la partie, donné par le rapport du Companion ; null sans rapport
    List<ChampionJoueResponse> ChampionsJoues, // vide sans rapport du Companion
    RapportEnRevueResponse? RapportEnRevue, // le rapport du Companion qui attend la décision de l'organisateur
    DateTime? StartedAt // le Début de la Game (fin de la Draft de champions, ADR 0017), null avant
)
{
    /// <param name="joueursConnus">Les joueurs enregistrés, par PUUID, pour reconnaître les joueurs inattendus d'un rapport en revue.</param>
    public static SeriesGameResponse From(CustomGame game, IReadOnlyDictionary<string, Player> joueursConnus) => new(
        Id: game.Id,
        Status: game.Status,
        TeamASide: game.TeamASide,
        TeamBSide: game.TeamBSide,
        Winner: game.WinningTeam,
        TeamA: game.ParticipantsOf(Equipes.A).Select(p => GameParticipantDto.From(p.Player)).ToList(),
        TeamB: game.ParticipantsOf(Equipes.B).Select(p => GameParticipantDto.From(p.Player)).ToList(),
        RiotMatchId: game.RiotMatchId,
        ChampionsJoues: game.Participants
            .Where(p => ValeursEffectives.Champion(p) != null)
            .OrderBy(p => p.PlayerId)
            .Select(p => new ChampionJoueResponse(p.PlayerId, ValeursEffectives.Champion(p)!))
            .ToList(),
        RapportEnRevue: game.RapportEnRevue is { } revue
            ? RapportEnRevueResponse.From(revue, game, joueursConnus)
            : null,
        StartedAt: game.StartedAt);
}

/// <summary>
/// Un rapport du Companion en revue : ses joueurs détectés ne sont pas la composition de la game.
/// L'écart sépare les participants absents du rapport, les joueurs détectés inattendus et les
/// participants détectés sur l'autre Side. Accepté, il termine la game avec l'Équipe gagnante.
/// </summary>
public record RapportEnRevueResponse(
    int Id,
    string RiotMatchId,
    DateTime RecuLe,
    string EquipeGagnante, // "A" | "B" : l'Équipe qui gagne la game si le rapport est accepté
    List<GameParticipantDto> Absents,
    List<JoueurInattenduResponse> Inattendus,
    List<GameParticipantDto> MauvaisSide)
{
    public static RapportEnRevueResponse From(RapportCompanion revue, CustomGame game, IReadOnlyDictionary<string, Player> joueursConnus)
    {
        var rapport = revue.Rapport();
        var ecart = rapport.EcartAvec(game);
        var participants = game.Participants.ToDictionary(p => p.PlayerId, p => p.Player);
        return new RapportEnRevueResponse(
            Id: revue.Id,
            RiotMatchId: revue.RiotMatchId,
            RecuLe: revue.RecuLe,
            EquipeGagnante: rapport.EquipeGagnanteDans(game),
            Absents: ecart.Absents.Select(id => GameParticipantDto.From(participants[id])).ToList(),
            Inattendus: rapport.Joueurs
                .Where(j => ecart.Inattendus.Contains(j.Puuid))
                .Select(j => new JoueurInattenduResponse(
                    j.Puuid, j.RiotId, j.Side, j.ChampionId,
                    joueursConnus.TryGetValue(j.Puuid, out var joueur) ? GameParticipantDto.From(joueur) : null))
                .ToList(),
            MauvaisSide: ecart.MauvaisSide.Select(id => GameParticipantDto.From(participants[id])).ToList());
    }
}

/// <summary>
/// Un joueur détecté par le rapport qui n'est pas participant de la game : son PUUID, son Side, son
/// champion joué, son RiotId (Nom#TAG) quand Shameless a pu le retrouver, et le joueur Shameless de ce
/// PUUID s'il est enregistré (null sinon).
/// </summary>
public record JoueurInattenduResponse(string Puuid, string? RiotId, string Side, string ChampionId, GameParticipantDto? Joueur);

/// <summary>Le champion réellement joué par un participant de la game (id Data Dragon, ex. "LeeSin").</summary>
public record ChampionJoueResponse(int PlayerId, string ChampionId);
