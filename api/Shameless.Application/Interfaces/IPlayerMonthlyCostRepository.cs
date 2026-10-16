using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Repositories;

public interface IPlayerMonthlyCostRepository
{
    /// <summary>L'historique de cost d'un Joueur, du plus ancien mois au plus récent.</summary>
    Task<List<PlayerMonthlyCost>> GetByPlayerAsync(int playerId);

    /// <summary>Enregistre un mois nouveau ou modifié (suivi par le contexte).</summary>
    Task SaveAsync(PlayerMonthlyCost monthlyCost);
}
