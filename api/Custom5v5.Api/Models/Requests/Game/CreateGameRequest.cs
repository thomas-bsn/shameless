namespace Custom5v5.Api.Models.Requests.Game;

public sealed record CreateGameRequest(
    List<int> BlueTeam,
    List<int> RedTeam,
    int? SeriesId,
    int? BoFormat
);