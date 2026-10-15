namespace Shameless.Api.Models.Responses.Game;

public sealed record SetGameResultResponse(
    int GameId,
    string? Winner, // Équipe gagnante : "A" | "B"
    string TeamASide,
    string TeamBSide,
    string Status
);
