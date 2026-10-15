namespace Shameless.Api.Models.Responses.Player;

public sealed record PlayerCostResponse(
    int PlayerId,
    string Name,
    decimal Cost,
    string Tier,
    int Mmr,
    int BaseMMR,
    decimal WinrateAdjustment
);