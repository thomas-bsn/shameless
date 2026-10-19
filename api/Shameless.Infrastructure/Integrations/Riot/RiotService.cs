using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Application.DTOs.Riot;
using Shameless.Application.Interfaces;
using Shameless.Application.Riot;
using Shameless.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly.CircuitBreaker;

namespace Shameless.Infrastructure.Integrations.Riot;

public sealed class RiotService : IRiotService
{
    private readonly HttpClient _http;
    private readonly RiotApiOptions _options;
    private readonly ILogger<RiotService> _logger;

    public RiotService(
        HttpClient http,
        IOptions<RiotApiOptions> options,
        ILogger<RiotService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetPuuidFromRiotIdAsync(string riotId)
    {
        var (gameName, tagLine) = SplitRiotId(riotId);
        try
        {
            var res = await GetAsync<AccountResponse>($"riot/account/v1/accounts/by-riot-id/{gameName}/{tagLine}");
            return res?.puuid;
        }
        catch (RiotException ex) when (ex.Cause == CauseEchecRiot.Introuvable)
        {
            _logger.LogWarning("Riot account not found: {GameName}#{TagLine}", gameName, tagLine);
            return null;
        }
    }

    public async Task<string?> GetRiotIdFromPuuidAsync(string puuid)
    {
        try
        {
            var res = await GetAsync<AccountResponse>($"riot/account/v1/accounts/by-puuid/{Uri.EscapeDataString(puuid)}");
            return res == null ? null : $"{res.gameName}#{res.tagLine}";
        }
        catch (RiotException ex) when (ex.Cause == CauseEchecRiot.Introuvable)
        {
            _logger.LogWarning("Riot account not found for PUUID: {PUUID}", puuid);
            return null;
        }
    }

    /// <summary>
    /// Le rôle principal sur les 15 dernières parties de SoloQ. Null seulement si Riot répond qu'il n'y a pas
    /// de partie. Toute erreur de Riot (liste ou partie) lève <see cref="RiotException"/> : pas de rôle calculé
    /// sur des données partielles. Seule une partie disparue (404) est ignorée.
    /// </summary>
    public async Task<string?> GetMainRoleFromPuuidAsync(string puuid)
    {
        var matchIds = await GetAsync<List<string>>($"lol/match/v5/matches/by-puuid/{Uri.EscapeDataString(puuid)}/ids?queue=420&count=15");
        if (matchIds == null || matchIds.Count == 0) return null;

        var roleCounts = new Dictionary<string, int>();
        foreach (var matchId in matchIds)
        {
            MatchResponse? match;
            try { match = await GetAsync<MatchResponse>($"lol/match/v5/matches/{matchId}"); }
            catch (RiotException ex) when (ex.Cause == CauseEchecRiot.Introuvable)
            {
                _logger.LogWarning("Match {MatchId} not found, ignored for main role detection", matchId);
                continue;
            }

            var role = match?.Info?.Participants?.FirstOrDefault(p => p.Puuid == puuid)?.TeamPosition;
            if (string.IsNullOrWhiteSpace(role)) continue;
            roleCounts[role] = roleCounts.GetValueOrDefault(role) + 1;
        }

        return roleCounts.Count == 0 ? null : roleCounts.MaxBy(x => x.Value).Key;
    }

    public async Task<RiotRanksDto> GetRanksFromPuuidAsync(string puuid)
    {
        var leagues = await GetAsync<List<LeagueResponse>>($"{_options.RegionalBaseUrl}/lol/league/v4/entries/by-puuid/{Uri.EscapeDataString(puuid)}");
        return new RiotRanksDto(Solo: Rank(leagues, "RANKED_SOLO_5x5"), Flex: Rank(leagues, "RANKED_FLEX_SR"));
    }

    /// <summary>
    /// Un GET sur Riot ; toute réponse non réussie ou tout échec de transport devient une
    /// <see cref="RiotException"/> avec sa cause.
    /// </summary>
    private async Task<T?> GetAsync<T>(string url)
    {
        try
        {
            using var res = await _http.GetAsync(url);
            if (!res.IsSuccessStatusCode)
            {
                var statut = (int)res.StatusCode;
                var cause = res.StatusCode switch
                {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => CauseEchecRiot.CleRefusee,
                    HttpStatusCode.TooManyRequests => CauseEchecRiot.LimiteAtteinte,
                    HttpStatusCode.NotFound => CauseEchecRiot.Introuvable,
                    HttpStatusCode.RequestTimeout => CauseEchecRiot.Injoignable,
                    >= HttpStatusCode.InternalServerError => CauseEchecRiot.Panne,
                    _ => CauseEchecRiot.Inattendue
                };
                var retryAfter = res.Headers.RetryAfter;
                var attente = retryAfter?.Delta ?? (retryAfter?.Date is { } date ? date - DateTimeOffset.UtcNow : null);
                var echec = new RiotException(cause, statut, cause == CauseEchecRiot.LimiteAtteinte ? attente : null);
                _logger.LogWarning("Riot answered {Status} for {Url}: {Cause}", statut, url, echec.Message);
                throw echec;
            }

            await Task.Delay(_options.RateLimitDelayMs);
            return await res.Content.ReadFromJsonAsync<T>();
        }
        catch (RiotException) { throw; }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or BrokenCircuitException)
        {
            _logger.LogError(ex, "Riot unreachable for {Url}", url);
            throw new RiotException(CauseEchecRiot.Injoignable, inner: ex);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Unreadable Riot response for {Url}", url);
            throw new RiotException(CauseEchecRiot.Inattendue, inner: ex);
        }
    }

    private static RiotRankDto? Rank(List<LeagueResponse>? leagues, string queueType) =>
        leagues?.FirstOrDefault(x => x.QueueType == queueType) is { } league
            ? new RiotRankDto { Tier = league.Tier, Division = league.Rank, LP = league.LeaguePoints }
            : null;

    private static (string GameName, string TagLine) SplitRiotId(string riotId)
    {
        var parts = riotId.Split('#');
        if (parts.Length != 2) throw new ArgumentException("Format invalide, attendu: name#tag");
        return (parts[0], parts[1]);
    }
}
