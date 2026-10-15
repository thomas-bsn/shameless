using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

public interface IBestOfSeriesRepository
{
    Task<BestOfSeries> AddAsync(BestOfSeries series);
    Task<BestOfSeries?> GetByIdAsync(int id);

    /// <summary>La Série avec sa composition courante et ses games (participants et joueurs), games triées par id.</summary>
    Task<BestOfSeries?> GetWithGamesAsync(int id);

    /// <summary>Ids des Séries en cours, de la plus ancienne à la plus récente.</summary>
    Task<List<int>> GetEnCoursIdsAsync();

    /// <summary>Ids des Séries d'une Soirée, de la plus ancienne à la plus récente.</summary>
    Task<List<int>> GetIdsBySoireeAsync(int soireeId);

    /// <summary>Marque la Série pour suppression, avec sa composition ; effectif au prochain SaveChangesAsync.</summary>
    void Remove(BestOfSeries series);

    Task SaveChangesAsync();
}
