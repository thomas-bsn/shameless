using Shameless.Application.Services;

namespace Shameless.Application.Cost;

/// <summary>
/// Les seuils, poids et bornes du cost mensuel : le seul endroit où ils sont fixés (ADR 0012).
/// </summary>
public static class ReglesDuCost
{
    /// <summary>Nombre de Games terminées dans le mois à partir duquel le cost évolue.</summary>
    public const int GamesMinimum = 5;

    /// <summary>Bornes de l'échelle du cost (tiers D à SS).</summary>
    public const decimal CostMinimum = 1m;
    public const decimal CostMaximum = 6m;

    /// <summary>
    /// Variation maximale du cost sur un mois, par rapport au cost de départ : atteinte avec une note
    /// moyenne de 100 et 100 % de victoires (ou 0 et 0 %).
    /// </summary>
    public const decimal VariationMaximale = 1m;

    /// <summary>Part de la performance individuelle dans la variation : plus que les résultats.</summary>
    public const decimal PoidsPerformance = 0.7m;

    /// <summary>Part des résultats du mois (winrate) dans la variation.</summary>
    public const decimal PoidsResultats = 0.3m;

    /// <summary>La note d'une game « moyenne pour le rôle » (ADR 0011) : ni bonus ni malus.</summary>
    public const decimal NoteNeutre = 50m;

    /// <summary>Le winrate qui ne fait ni monter ni descendre le cost.</summary>
    public const decimal WinrateNeutre = 0.5m;

    /// <summary>Le tier (D à SS) d'un cost, arrondi à l'unité comme le front : 2,5 → B.</summary>
    public static string TierDuCost(decimal cost) =>
        MMRCalculationService.GetTierLabel(Math.Clamp(Math.Round(cost, MidpointRounding.AwayFromZero), CostMinimum, CostMaximum));
}
