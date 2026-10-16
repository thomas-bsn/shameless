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

    /// <summary>L'identifiant Riot du rapport en revue de la game, tel qu'enregistré en base, ou null.</summary>
    Task<string?> GetRiotMatchIdEnRevueAsync(int gameId);

    /// <summary>
    /// La game rattachée à cet identifiant Riot, avec sa Série, telle qu'enregistrée en base (sans
    /// les changements non enregistrés de la commande en cours), ou null.
    /// </summary>
    Task<CustomGame?> GetByRiotMatchIdAsync(string riotMatchId);
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