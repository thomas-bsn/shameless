namespace Custom5v5.Api.Contracts.Matches;

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