using Shameless.Application.DTOs.Riot;

namespace Shameless.Application.Interfaces;

public interface IRiotService
{
    Task<string?> GetPuuidFromRiotIdAsync(string riotId);

    /// <summary>Les rangs actuels du compte, par file classée (SoloQ et Flex) ; null pour une file non classée.</summary>
    Task<RiotRanksDto> GetRanksFromPuuidAsync(string puuid);
    Task<string?> GetMainRoleFromPuuidAsync(string puuid);
    Task<string?> GetRiotIdFromPuuidAsync(string puuid);
}
