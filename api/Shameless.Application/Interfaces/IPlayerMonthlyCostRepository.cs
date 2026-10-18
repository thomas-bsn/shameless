using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Repositories;

public interface IPlayerMonthlyCostRepository
{
    /// <summary>L'historique de cost d'un Joueur, du plus ancien mois au plus récent.</summary>
    Task<List<PlayerMonthlyCost>> GetByPlayerAsync(int playerId);

    /// <summary>
    /// Pour chaque Joueur, le cost final du dernier mois enregistré strictement avant le mois donné
    /// (premier jour du mois) ; les Joueurs sans mois antérieur sont absents.
    /// </summary>
    Task<Dictionary<int, decimal>> GetCostsFinauxAvantAsync(DateOnly mois);

    /// <summary>
    /// Enregistre le mois du Joueur : l'ajoute, ou remplace ses valeurs s'il existe déjà, même s'il
    /// vient d'être ajouté par une autre game terminée en même temps.
    /// </summary>
    Task UpsertAsync(PlayerMonthlyCost monthlyCost);
}