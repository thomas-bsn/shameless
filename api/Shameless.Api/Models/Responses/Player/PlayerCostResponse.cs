using Shameless.Application.Cost;
using Shameless.Application.Entities;
using Shameless.Application.Services;

namespace Shameless.Api.Models.Responses.Player;

/// <summary>
/// Le cost d'un Joueur pour le mois en cours et le détail de son calcul (ADR 0012) : cost de départ
/// et sa source, Games terminées, winrate, note moyenne, et la part de chacun dans la variation.
/// </summary>
public sealed record PlayerCostResponse(
    int PlayerId,
    string Name,
    DateOnly Mois,
    decimal Cost,
    string Tier,
    decimal CostDeDepart,
    SourceDuCostDeDepart SourceDuCostDeDepart,
    int GamesTerminees,
    int Victoires,
    decimal? Winrate,
    int GamesNotees,
    decimal? NoteMoyenne,
    bool Evolue,
    int GamesMinimum,
    decimal AjustementPerformance,
    decimal AjustementResultats)
{
    public static PlayerCostResponse From(CostDuMois cost)
    {
        var calcul = cost.Calcul;
        return new PlayerCostResponse(
            cost.Player.Id, cost.Player.Prenom, cost.Mois, calcul.Cost, TierDuCost(calcul.Cost),
            calcul.Depart.Cost, calcul.Depart.Source, calcul.GamesTerminees, calcul.Victoires, calcul.Winrate,
            calcul.GamesNotees, calcul.NoteMoyenne, calcul.Evolue, ReglesDuCost.GamesMinimum,
            calcul.AjustementPerformance, calcul.AjustementResultats);
    }

    /// <summary>Le tier du cost arrondi à l'unité (comme le front : 2,5 → B).</summary>
    public static string TierDuCost(decimal cost) =>
        MMRCalculationService.GetTierLabel(Math.Clamp(Math.Round(cost, MidpointRounding.AwayFromZero), ReglesDuCost.CostMinimum, ReglesDuCost.CostMaximum));
}

/// <summary>L'historique de cost d'un Joueur, du plus ancien mois au plus récent.</summary>
public sealed record PlayerCostHistoryResponse(int PlayerId, IReadOnlyList<CostMensuelHistorique> Mois);

/// <summary>
/// Le cost d'un mois conservé dans PlayerMonthlyCost : cost final (en cours pour le mois actuel),
/// cost de départ (null pour un mois repris de l'ancienne application) et Games terminées.
/// </summary>
public sealed record CostMensuelHistorique(
    DateOnly Mois,
    decimal Cost,
    decimal? CostDeDepart,
    int GamesTerminees,
    int Victoires,
    decimal? NoteMoyenne)
{
    public static CostMensuelHistorique From(PlayerMonthlyCost mois) =>
        new(DateOnly.FromDateTime(mois.Month), mois.FinalCost, mois.CostDeDepart, mois.GamesPlayed, mois.Wins, mois.NoteMoyenne);
}
