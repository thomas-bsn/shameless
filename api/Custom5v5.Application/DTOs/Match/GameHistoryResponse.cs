namespace Custom5v5.Application.DTOs.Match;

public record GameHistoryResponse(
    int Id,
    int? SeriesId,
    DateTime PlayedAt,
    string? Winner,
    List<GameParticipantDto> BlueTeam,
    List<GameParticipantDto> RedTeam
);

public record GameParticipantDto(
    string Prenom,
    string RiotId,
    string? RankTier,
    int? RankDivision,
    decimal? Cost
);