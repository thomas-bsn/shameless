using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

public interface IBestOfSeriesRepository
{
    /// <summary>Ajoute la Série (avec sa composition) ; effectif au prochain enregistrement.</summary>
    void Add(BestOfSeries series);

    Task<BestOfSeries?> GetByIdAsync(int id);

    /// <summary>La Série avec sa composition courante et ses games (participants et joueurs), games triées par id.</summary>
    Task<BestOfSeries?> GetWithGamesAsync(int id);

    /// <summary>Les Séries en cours avec leurs games, de la plus ancienne à la plus récente.</summary>
    Task<List<BestOfSeries>> GetEnCoursWithGamesAsync();

    /// <summary>Les Séries d'une Soirée avec leurs games, de la plus ancienne à la plus récente.</summary>
    Task<List<BestOfSeries>> GetBySoireeWithGamesAsync(int soireeId);

    /// <summary>La Série avec ses liens d'accès.</summary>
    Task<BestOfSeries?> GetWithLiensAsync(int id);

    /// <summary>Le lien d'accès de ce secret, avec sa Série, ou null.</summary>
    Task<LienAcces?> GetLienAsync(string secret);

    /// <summary>La Série de ce code Companion, avec sa composition courante et ses joueurs, ou null.</summary>
    Task<BestOfSeries?> GetByCodeCompanionAsync(string code);

    /// <summary>Marque la Série pour suppression, avec sa composition ; effectif au prochain enregistrement.</summary>
    void Remove(BestOfSeries series);

    /// <summary>
    /// Enregistre les changements en une transaction. Renvoie false si une commande simultanée a
    /// modifié une Série entre-temps (voir <see cref="BestOfSeries.Version"/>) : rien n'est enregistré.
    /// </summary>
    Task<bool> TrySaveChangesAsync();
}
