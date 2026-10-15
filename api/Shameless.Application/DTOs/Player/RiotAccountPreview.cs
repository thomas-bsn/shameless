namespace Shameless.Application.DTOs.Players;

/// <summary>Un compte Riot avant son ajout comme Joueur.</summary>
public sealed record RiotAccountPreview(
    string RiotId,
    string? RankTier,
    int? RankDivision,
    int? Lp,
    string? MainRole,
    bool AlreadyRegistered
);
