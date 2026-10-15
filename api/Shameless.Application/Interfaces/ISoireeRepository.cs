using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

public interface ISoireeRepository
{
    Task<Soiree?> GetOpenAsync();
    Task<Soiree?> GetByIdAsync(int id);

    /// <summary>Ajoute la Soirée. Renvoie false si la base refuse une deuxième Soirée ouverte.</summary>
    Task<bool> TryAddAsync(Soiree soiree);

    Task SaveChangesAsync();
}
