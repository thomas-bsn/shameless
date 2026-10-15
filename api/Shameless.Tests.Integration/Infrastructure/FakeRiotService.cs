using Shameless.Application.DTOs.Riot;
using Shameless.Application.Interfaces;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>Faux Riot : chaque compte est déclaré par le test avec son rang.</summary>
public sealed class FakeRiotService : IRiotService
{
    private readonly Dictionary<string, (string Puuid, RiotRankDto? Rank, string? MainRole)> _accounts = new();

    public void AddAccount(string riotId, string tier, string division, int lp = 0, string? mainRole = "MIDDLE") =>
        _accounts[riotId] = ($"puuid-{riotId}", new RiotRankDto { Tier = tier, Division = division, LP = lp }, mainRole);

    public void Reset() => _accounts.Clear();

    public Task<string?> GetPuuidFromRiotIdAsync(string riotId) =>
        Task.FromResult(_accounts.TryGetValue(riotId, out var a) ? a.Puuid : null);

    public Task<RiotRankDto?> GetRankFromPuuidAsync(string puuid) =>
        Task.FromResult(_accounts.Values.FirstOrDefault(a => a.Puuid == puuid).Rank);

    public async Task<RiotRankDto?> GetRankAsync(string riotId) =>
        await GetPuuidFromRiotIdAsync(riotId) is { } puuid ? await GetRankFromPuuidAsync(puuid) : null;

    public Task<string?> GetMainRoleFromPuuidAsync(string puuid) =>
        Task.FromResult(_accounts.Values.FirstOrDefault(a => a.Puuid == puuid).MainRole);

    public Task<string?> GetRiotIdFromPuuidAsync(string puuid) =>
        Task.FromResult<string?>(_accounts.FirstOrDefault(a => a.Value.Puuid == puuid).Key);
}
