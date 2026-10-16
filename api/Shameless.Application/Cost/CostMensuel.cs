using Shameless.Application.Services;

namespace Shameless.Application.Cost;

/// <summary>Une Game terminée du mois pour un Joueur : son résultat et sa note de performance (null sans stats).</summary>
public sealed record GameDuMois(bool Gagnee, int? Note);

/// <summary>D'où vient le cost de départ d'un mois.</summary>
public enum SourceDuCostDeDepart
{
    /// <summary>Le cost final du dernier mois conservé dans l'historique.</summary>
    MoisPrecedent,

    /// <summary>Pas d'historique de cost : le Rang SoloQ sert d'indicateur initial.</summary>
    RangSoloQ,

    /// <summary>Le cost fixé par un admin pour ce mois.</summary>
    FixeParAdmin
}

/// <summary>Le cost d'un Joueur au début d'un mois, et sa provenance.</summary>
public sealed record CostDeDepart(decimal Cost, SourceDuCostDeDepart Source)
{
    public static CostDeDepart DuMoisPrecedent(decimal costFinal) => new(costFinal, SourceDuCostDeDepart.MoisPrecedent);

    public static CostDeDepart FixeParAdmin(decimal cost) => new(cost, SourceDuCostDeDepart.FixeParAdmin);

    /// <summary>
    /// Le cost de départ d'un Joueur sans historique, lu sur son Rang SoloQ actuel (pas le Peak) :
    /// barème MMR du rang, converti en cost (non classé : 1).
    /// </summary>
    public static CostDeDepart DuRangSoloQ(string? tier, int? division) =>
        new(MMRCalculationService.MMRToCost(MMRCalculationService.GetRankMMR(tier, division)), SourceDuCostDeDepart.RangSoloQ);
}

/// <summary>
/// Le cost d'un Joueur pour un mois (ADR 0012), calculé depuis son cost de départ et ses Games
/// terminées du mois, avec le détail du calcul.
/// </summary>
/// <param name="Cost">Le cost du mois, de 1 à 6 au dixième.</param>
/// <param name="Evolue">
/// Vrai à partir de <see cref="ReglesDuCost.GamesMinimum"/> Games terminées dont au moins une notée.
/// </param>
/// <param name="Winrate">Pourcentage de victoires (0 à 100, au dixième), null sans Game.</param>
/// <param name="NoteMoyenne">Moyenne des notes de performance (au dixième), null sans Game notée.</param>
/// <param name="GamesNotees">Nombre de Games ayant une note de performance.</param>
/// <param name="AjustementPerformance">Part de la variation due à la note moyenne (au centième).</param>
/// <param name="AjustementResultats">Part de la variation due au winrate (au centième).</param>
public sealed record CostMensuel(
    decimal Cost,
    CostDeDepart Depart,
    int GamesTerminees,
    int Victoires,
    decimal? Winrate,
    int GamesNotees,
    decimal? NoteMoyenne,
    bool Evolue,
    decimal AjustementPerformance,
    decimal AjustementResultats)
{
    public static CostMensuel Calculer(CostDeDepart depart, IReadOnlyCollection<GameDuMois> games)
    {
        var victoires = games.Count(g => g.Gagnee);
        var ratio = games.Count == 0 ? (decimal?)null : (decimal)victoires / games.Count;
        var notes = games.Where(g => g.Note is not null).Select(g => (decimal)g.Note!.Value).ToList();
        var noteMoyenne = notes.Count == 0 ? (decimal?)null : notes.Average();
        // Sans aucune note, le winrate seul ne fait pas évoluer le cost (ADR 0012).
        var evolue = games.Count >= ReglesDuCost.GamesMinimum && noteMoyenne is not null;

        var performance = evolue
            ? Ajustement(ReglesDuCost.PoidsPerformance, (noteMoyenne!.Value - ReglesDuCost.NoteNeutre) / ReglesDuCost.NoteNeutre)
            : 0m;
        var resultats = evolue && ratio is not null
            ? Ajustement(ReglesDuCost.PoidsResultats, (ratio.Value - ReglesDuCost.WinrateNeutre) / ReglesDuCost.WinrateNeutre)
            : 0m;
        var cost = Math.Clamp(
            Math.Round(depart.Cost + performance + resultats, 1, MidpointRounding.AwayFromZero),
            ReglesDuCost.CostMinimum, ReglesDuCost.CostMaximum);

        return new CostMensuel(
            cost, depart, games.Count, victoires,
            ratio is null ? null : Arrondi(ratio.Value * 100, 1),
            notes.Count,
            noteMoyenne is null ? null : Arrondi(noteMoyenne.Value, 1),
            evolue, Arrondi(performance, 2), Arrondi(resultats, 2));
    }

    /// <summary>Écart à la neutralité (entre −1 et 1) × poids × variation maximale.</summary>
    private static decimal Ajustement(decimal poids, decimal ecart) =>
        ReglesDuCost.VariationMaximale * poids * Math.Clamp(ecart, -1m, 1m);

    private static decimal Arrondi(decimal valeur, int decimales) =>
        Math.Round(valeur, decimales, MidpointRounding.AwayFromZero);
}
