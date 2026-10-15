namespace Shameless.Application.DTOs.Match;

/// <summary>
/// Une Série et ses deux Équipes. La composition d'une Équipe est celle de sa dernière game ;
/// chaque game donne le Side de chaque Équipe et sa composition pour cette game.
/// </summary>
public record SeriesResponse(
    int SeriesId,
    int Format,
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
);
