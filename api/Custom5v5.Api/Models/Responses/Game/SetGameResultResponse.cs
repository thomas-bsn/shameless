namespace Custom5v5.Api.Models.Responses.Game;

public sealed record SetGameResultResponse(
    int GameId,
    string? Winner,
    string Status
);