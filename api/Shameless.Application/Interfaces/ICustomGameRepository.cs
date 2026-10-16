using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

public interface ICustomGameRepository
{
    /// <summary>Ajoute la game (avec ses participants) ; effectif au prochain enregistrement.</summary>
    void Add(CustomGame game);

    Task<CustomGame?> GetByIdAsync(int id);
    Task<CustomGame?> GetByIdWithParticipantsAsync(int id);

    /// <summary>Un rapport du Companion mis en revue, avec sa game ; null s'il n'existe pas.</summary>
    Task<RapportCompanion?> GetRapportAsync(int id);
    Task<List<CustomGame>> GetByMonthAsync(DateTime month);
    Task<List<CustomGameParticipant>> GetPlayerGamesForMonthAsync(int playerId, DateTime month);

    /// <summary>
    /// Enregistre les changements en une transaction. Renvoie false si une commande simultanée a
    /// modifié une game ou sa Série entre-temps : rien n'est enregistré.
    /// </summary>
    Task<bool> TrySaveChangesAsync();

    Task<List<CustomGame>> GetPendingAsync();
    Task<List<CustomGame>> GetByYearMonthAsync(int year, int month);
    Task<List<CustomGame>> GetBySeriesIdAsync(int seriesId);

    /// <summary>Marque la game pour suppression, avec ses participants ; effectif au prochain SaveChangesAsync.</summary>
    void Remove(CustomGame game);
}