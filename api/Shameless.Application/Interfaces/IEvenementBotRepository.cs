using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

/// <summary>L'outbox des webhooks vers le bot Discord.</summary>
public interface IEvenementBotRepository
{
    /// <summary>Ajoute l'événement ; enregistré avec les autres changements de la commande en cours.</summary>
    void Add(EvenementBot evenement);

    /// <summary>
    /// Les événements à livrer dont l'heure de tentative est passée, dans l'ordre où ils sont survenus,
    /// avec de quoi relire leur snapshot : Série et composition (comptes Discord liés), ou Game avec
    /// participants (comptes liés) et sa Série (games, composition).
    /// </summary>
    Task<List<EvenementBot>> GetALivrerAsync(DateTime maintenant);

    Task<bool> TrySaveChangesAsync();
}
