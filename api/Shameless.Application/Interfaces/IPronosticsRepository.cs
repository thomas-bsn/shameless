using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

/// <summary>Les pronostics des Spectateurs, par Série et numéro de Game.</summary>
public interface IPronosticsRepository
{
    /// <summary>Les pronostics sur la Game de ce numéro dans la Série.</summary>
    Task<List<Pronostic>> GetDeLaGameAsync(int seriesId, int numeroGame);

    Task<Pronostic?> GetAsync(int seriesId, int numeroGame, long pronostiqueurDiscordId);

    /// <summary>La Série (lecture seule) avec sa composition courante (comptes liés) et ses games.</summary>
    Task<BestOfSeries?> GetSerieAsync(int seriesId);

    void Add(Pronostic pronostic);

    void Remove(Pronostic pronostic);

    Task<bool> TrySaveChangesAsync();
}
