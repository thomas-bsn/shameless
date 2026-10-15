using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

public interface IPickerSessionRepository
{
    /// <summary>La session de Picker de la Soirée, avec ses joueurs, ou null.</summary>
    Task<PickerSession?> GetBySoireeAsync(int soireeId);

    /// <summary>Ajoute la session. Renvoie false si la Soirée en a déjà une.</summary>
    Task<bool> TryAddAsync(PickerSession session);

    /// <summary>Marque la session pour suppression ; effectif au prochain enregistrement.</summary>
    void Remove(PickerSession session);

    /// <summary>
    /// Enregistre les changements en une transaction. Renvoie false si une commande simultanée a
    /// modifié la session entre-temps (voir <see cref="PickerSession.Version"/>) : rien n'est enregistré.
    /// </summary>
    Task<bool> TrySaveChangesAsync();
}
