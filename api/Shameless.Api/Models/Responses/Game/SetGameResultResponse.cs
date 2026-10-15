namespace Shameless.Api.Models.Responses.Game;

public sealed record SetGameResultResponse(
    int GameId,
    string? Winner,
    string Status
);