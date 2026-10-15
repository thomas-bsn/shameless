namespace Shameless.Api.Models.Requests.Game;

public sealed record CreateGameRequest(
    List<int> BlueTeam,
    List<int> RedTeam,
    int? SeriesId,
    int? BoFormat
);