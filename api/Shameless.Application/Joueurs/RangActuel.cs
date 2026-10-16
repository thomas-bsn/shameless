namespace Shameless.Application.Joueurs;

/// <summary>
/// Un rang Riot actuel dans une file classée (SoloQ ou Flex). Comparable : un rang est plus grand
/// qu'un autre s'il est meilleur (tier, puis division, puis LP).
/// </summary>
/// <param name="Tier">Tier en majuscules (ex. « GOLD »).</param>
/// <param name="Division">1 à 4 (1 = I), null à partir de Master.</param>
public sealed record RangActuel(string Tier, int? Division, int Lp) : IComparable<RangActuel>
{
    /// <summary>Les tiers Riot, du plus bas au plus haut.</summary>
    private static readonly string[] Tiers =
        ["IRON", "BRONZE", "SILVER", "GOLD", "PLATINUM", "EMERALD", "DIAMOND", "MASTER", "GRANDMASTER", "CHALLENGER"];

    private const int IndexDeMaster = 7;

    /// <summary>Le rang lu sur le Joueur, null s'il n'est pas classé (tier absent, « UNRANKED » ou inconnu).</summary>
    public static RangActuel? De(string? tier, int? division, int? lp)
    {
        var index = tier == null ? -1 : Array.IndexOf(Tiers, tier.ToUpperInvariant());
        return index < 0 ? null : new RangActuel(Tiers[index], index >= IndexDeMaster ? null : division, lp ?? 0);
    }

    public int CompareTo(RangActuel? autre)
    {
        if (autre is null) return 1;
        var tier = Array.IndexOf(Tiers, Tier).CompareTo(Array.IndexOf(Tiers, autre.Tier));
        if (tier != 0) return tier;
        // Division I (1) au-dessus de la IV (4).
        var division = (autre.Division ?? 0).CompareTo(Division ?? 0);
        return division != 0 ? division : Lp.CompareTo(autre.Lp);
    }
}
