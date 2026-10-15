namespace Shameless.Api.Models.Responses.Game;

public sealed record CreateGameResponse(
    int GameId,
    DateTime PlayedAt,
    int? SeriesId,
    string Message
);