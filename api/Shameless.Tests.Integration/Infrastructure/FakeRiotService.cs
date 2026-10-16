using Shameless.Application.DTOs.Riot;
using Shameless.Application.Interfaces;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>Faux Riot : chaque compte est déclaré par le test avec ses rangs par file classée.</summary>
public sealed class FakeRiotService : IRiotService
{
    private readonly Dictionary<string, (string Puuid, RiotRanksDto Ranks, string? MainRole)> _accounts = new();

    /// <summary>Déclare un compte ; un tier null : pas classé dans la file (SoloQ ou Flex).</summary>
    public void AddAccount(string riotId, string? tier, string? division, int lp = 0, string? mainRole = "MIDDLE",
        string? flexTier = null, string? flexDivision = null, int flexLp = 0) =>
        _accounts[riotId] = ($"puuid-{riotId}",
            new RiotRanksDto(Rank(tier, division, lp), Rank(flexTier, flexDivision, flexLp)),
            mainRole);

    private static RiotRankDto? Rank(string? tier, string? division, int lp) =>
        tier == null ? null : new RiotRankDto { Tier = tier, Division = division ?? "", LP = lp };

    public void Reset() => _accounts.Clear();

    public Task<string?> GetPuuidFromRiotIdAsync(string riotId) =>
        Task.FromResult(_accounts.TryGetValue(riotId, out var a) ? a.Puuid : null);

    public Task<RiotRanksDto> GetRanksFromPuuidAsync(string puuid) =>
        Task.FromResult(_accounts.Values.FirstOrDefault(a => a.Puuid == puuid).Ranks ?? new RiotRanksDto(null, null));

    public Task<string?> GetMainRoleFromPuuidAsync(string puuid) =>
        Task.FromResult(_accounts.Values.FirstOrDefault(a => a.Puuid == puuid).MainRole);

    public Task<string?> GetRiotIdFromPuuidAsync(string puuid) =>
        Task.FromResult<string?>(_accounts.FirstOrDefault(a => a.Value.Puuid == puuid).Key);
}
