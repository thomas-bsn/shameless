using Shameless.Application.Entities;

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
    bool AlternateSides,
    SeriesOutcome Outcome,
    string? WinningTeam, // "A" | "B" quand la Série est gagnée
    int TeamAWins,
    int TeamBWins,
    List<GameParticipantDto> TeamA,
    List<GameParticipantDto> TeamB,
    List<SeriesGameResponse> Games
);

public record SeriesGameResponse(
    int Id,
    string Status,
    string TeamASide,
    string TeamBSide,
    string? Winner,
    List<GameParticipantDto> TeamA,
    List<GameParticipantDto> TeamB
)
{
    public static SeriesGameResponse From(CustomGame game) => new(
        Id: game.Id,
        Status: game.Status,
        TeamASide: game.TeamASide,
        TeamBSide: game.TeamBSide,
        Winner: game.WinningTeam,
        TeamA: game.ParticipantsOf(Equipes.A).Select(p => GameParticipantDto.From(p.Player)).ToList(),
        TeamB: game.ParticipantsOf(Equipes.B).Select(p => GameParticipantDto.From(p.Player)).ToList());
}
