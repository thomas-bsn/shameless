using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

public interface ISoireeRepository
{
    Task<Soiree?> GetOpenAsync();
    Task<Soiree?> GetByIdAsync(int id);

    /// <summary>La Soirée ouverte avec ses séries et leurs games, ou null.</summary>
    Task<Soiree?> GetOpenWithSeriesAndGamesAsync();

    /// <summary>Ajoute la Soirée. Renvoie false si la base refuse une deuxième Soirée ouverte.</summary>
    Task<bool> TryAddAsync(Soiree soiree);

    Task SaveChangesAsync();
}
