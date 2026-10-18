namespace Shameless.Application.Services;

/// <summary>Ce que partagent les corrections d'une game terminée par un Administrateur (ADR 0009).</summary>
internal static class CorrectionsDeGame
{
    /// <summary>Le refus quand la base tranche une commande simultanée sur la game ou sa Série.</summary>
    public const string ModifieeEntreTemps =
        "Cette game ou sa Série vient d'être modifiée par une autre commande : rechargez-la et réessayez.";

    /// <summary>Recalcule le cost du mois de la game pour chacun des joueurs : leur note de performance en dépend.</summary>
    public static async Task RecalculerLesCostsAsync(CostCalculationService costs, IEnumerable<int> playerIds, DateTime playedAt)
    {
        var mois = DateOnly.FromDateTime(playedAt);
        foreach (var playerId in playerIds)
            await costs.RecalculatePlayerCost(playerId, mois);
    }
}
