using Shameless.Application.DTOs.Riot;

namespace Shameless.Application.Interfaces;

public interface IRiotService
{
    Task<string?> GetPuuidFromRiotIdAsync(string riotId);

    /// <summary>Les rangs actuels du compte, par file classée (SoloQ et Flex) ; null pour une file non classée.</summary>
    Task<RiotRanksDto> GetRanksFromPuuidAsync(string puuid);
    /// <summary>
    /// Le rôle principal sur les 15 dernières parties de SoloQ ; null s'il n'y a aucune partie lisible.
    /// Lève une exception quand Riot échoue : l'appelant garde alors l'ancien rôle.
    /// </summary>
    Task<string?> GetMainRoleFromPuuidAsync(string puuid);
    Task<string?> GetRiotIdFromPuuidAsync(string puuid);
}