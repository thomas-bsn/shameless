using Shameless.Application.Entities;

namespace Shameless.Application.Classements;

/// <summary>
/// Le classement SoloQ de l'Instance : les rangs Riot actuels, une liste par file classée (SoloQ et Flex).
/// Distinct du classement 5v5 ; le Peak n'y entre pas.
/// </summary>
public record ClassementSoloQ(List<LigneClassementSoloQ> Solo, List<LigneClassementSoloQ> Flex)
{
    private static readonly string[] Tiers =
        ["IRON", "BRONZE", "SILVER", "GOLD", "PLATINUM", "EMERALD", "DIAMOND", "MASTER", "GRANDMASTER", "CHALLENGER"];

    /// <summary>
    /// Classe les Joueurs du meilleur au moins bon rang dans chaque file. Un Joueur non classé dans une file
    /// garde sa ligne (rang null), en fin de liste, par prénom.
    /// </summary>
    public static ClassementSoloQ Calculer(IReadOnlyCollection<Player> joueurs) => new(
        Classer(joueurs, j => RangDe(j.RankTier, j.RankDivision, j.LP)),
        Classer(joueurs, j => RangDe(j.FlexRankTier, j.FlexRankDivision, j.FlexLp)));

    private static List<LigneClassementSoloQ> Classer(IEnumerable<Player> joueurs, Func<Player, RangSoloQ?> rang) =>
        joueurs
            .Select(j => (Joueur: j, Rang: rang(j)))
            .OrderByDescending(l => l.Rang is not null)
            .ThenByDescending(l => l.Rang?.TierIndex)
            .ThenBy(l => l.Rang?.Division ?? 0)
            .ThenByDescending(l => l.Rang?.Lp ?? 0)
            .ThenBy(l => l.Joueur.Prenom, StringComparer.OrdinalIgnoreCase)
            .Select(l => new LigneClassementSoloQ(
                l.Joueur.Id, l.Joueur.Prenom, l.Joueur.RiotId, l.Rang?.Tier, l.Rang?.Division, l.Rang?.Lp))
            .ToList();

    /// <summary>Le rang lu sur le Joueur, null s'il n'est pas classé (tier absent, « UNRANKED » ou inconnu).</summary>
    private static RangSoloQ? RangDe(string? tier, int? division, int? lp)
    {
        var index = tier == null ? -1 : Array.IndexOf(Tiers, tier.ToUpperInvariant());
        return index < 0 ? null : new RangSoloQ(Tiers[index], index, index >= 7 ? null : division, lp ?? 0);
    }

    private sealed record RangSoloQ(string Tier, int TierIndex, int? Division, int Lp);
}

/// <summary>La ligne d'un Joueur dans une file du classement SoloQ.</summary>
/// <param name="Tier">Tier en majuscules (ex. « GOLD »), null si non classé dans la file.</param>
/// <param name="Division">1 à 4 (1 = I), null à partir de Master ou si non classé.</param>
/// <param name="Lp">LP actuels, null si non classé.</param>
public record LigneClassementSoloQ(
    int PlayerId,
    string Prenom,
    string RiotId,
    string? Tier,
    int? Division,
    int? Lp);
