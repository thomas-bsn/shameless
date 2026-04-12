using Custom5v5.Application.DTOs.Riot;

namespace Custom5v5.Application.Interfaces;

public interface IRiotService
{
    Task<string?> GetPuuidFromRiotIdAsync(string riotId);
    Task<RiotRankDto?> GetRankFromPuuidAsync(string puuid);
    Task<RiotRankDto?> GetRankAsync(string riotId); // raccourci des deux
    Task<string?> GetMainRoleFromPuuidAsync(string puuid);
    Task<string?> GetRiotIdFromPuuidAsync(string puuid);
}