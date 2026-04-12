namespace Custom5v5.Api.Contracts.Matches;

public record RecordGameRequest(
    List<int> BlueTeam,
    List<int> RedTeam,
    int? SeriesId,
    int? BoFormat
);