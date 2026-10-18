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

    /// <summary>Vrai : l'API Riot ne répond plus pour les rangs (erreur réseau ou serveur).</summary>
    public bool RangsIndisponibles { get; set; }

    /// <summary>Les RiotId dont le calcul du rôle principal échoue (Riot injoignable pour ce compte).</summary>
    public HashSet<string> RolesIndisponibles { get; } = new();

    /// <summary>Quand elle est posée, la lecture des rangs attend sa libération : fige une actualisation en cours.</summary>
    public TaskCompletionSource? Barriere { get; set; }

    public void Reset()
    {
        _accounts.Clear();
        RolesIndisponibles.Clear();
        RangsIndisponibles = false;
        Barriere?.TrySetResult();
        Barriere = null;
    }

    public Task<string?> GetPuuidFromRiotIdAsync(string riotId) =>
        Task.FromResult(_accounts.TryGetValue(riotId, out var a) ? a.Puuid : null);

    public async Task<RiotRanksDto> GetRanksFromPuuidAsync(string puuid)
    {
        if (Barriere is { } barriere) await barriere.Task;
        if (RangsIndisponibles) throw new HttpRequestException("Riot indisponible");
        return _accounts.Values.FirstOrDefault(a => a.Puuid == puuid).Ranks ?? new RiotRanksDto(null, null);
    }

    public Task<string?> GetMainRoleFromPuuidAsync(string puuid)
    {
        var compte = _accounts.FirstOrDefault(a => a.Value.Puuid == puuid);
        return RolesIndisponibles.Contains(compte.Key ?? "")
            ? Task.FromException<string?>(new HttpRequestException("Riot indisponible"))
            : Task.FromResult(compte.Value.MainRole);
    }

    public Task<string?> GetRiotIdFromPuuidAsync(string puuid) =>
        Task.FromResult<string?>(_accounts.FirstOrDefault(a => a.Value.Puuid == puuid).Key);
}
