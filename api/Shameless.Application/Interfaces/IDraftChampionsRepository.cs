using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

public interface IDraftChampionsRepository
{
    /// <summary>Ajoute la Draft de champions ; effectif au prochain enregistrement.</summary>
    void Add(DraftChampions draft);

    /// <summary>La Draft de champions de la game, avec ses actions, sa game et la Série de la game (composition comprise).</summary>
    Task<DraftChampions?> GetByGameAsync(int gameId);

    /// <summary>La Draft de champions de la dernière game de la Série qui en a une, chargée comme <see cref="GetByGameAsync"/>.</summary>
    Task<DraftChampions?> GetDerniereOfSeriesAsync(int seriesId);

    /// <summary>
    /// Enregistre les changements en une transaction. Renvoie false si une commande simultanée a modifié
    /// la même Draft de champions entre-temps : rien n'est enregistré.
    /// </summary>
    Task<bool> TrySaveChangesAsync();
}
