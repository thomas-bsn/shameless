using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

public interface IPickerSessionRepository
{
    /// <summary>La session de Picker de la Soirée, avec ses joueurs, ou null.</summary>
    Task<PickerSession?> GetBySoireeAsync(int soireeId);

    /// <summary>Ajoute la session. Renvoie false si la Soirée en a déjà une.</summary>
    Task<bool> TryAddAsync(PickerSession session);

    Task RemoveAsync(PickerSession session);

    Task SaveChangesAsync();
}
