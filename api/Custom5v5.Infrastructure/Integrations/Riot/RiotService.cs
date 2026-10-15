using System.Net;
using System.Net.Http.Json;
using Custom5v5.Application.DTOs.Riot;
using Custom5v5.Application.Interfaces;
using Custom5v5.Infrastructure.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Custom5v5.Infrastructure.Integrations.Riot;

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

    public async Task<RiotRankDto?> GetRankAsync(string riotId)
    {
        var puuid = await GetPuuidFromRiotIdAsync(riotId);
        if (puuid == null) return null;

        return await GetRankFromPuuidAsync(puuid);
    }

    public async Task<string?> GetPuuidFromRiotIdAsync(string riotId)
    {
        var (gameName, tagLine) = SplitRiotId(riotId);
        return await GetPuuidAsync(gameName, tagLine);
    }

    private async Task<string?> GetPuuidAsync(string gameName, string tagLine)
    {
        try
        {
            var url = $"riot/account/v1/accounts/by-riot-id/{gameName}/{tagLine}";
            var res = await _http.GetFromJsonAsync<AccountResponse>(url);
            await Task.Delay(_options.RateLimitDelayMs);
            return res?.puuid;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Riot account not found: {GameName}#{TagLine}", gameName, tagLine);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch PUUID for {GameName}#{TagLine}", gameName, tagLine);
            throw;
        }
    }
    
    public async Task<string?> GetRiotIdFromPuuidAsync(string puuid)
    {
        try
        {
            var url = $"riot/account/v1/accounts/by-puuid/{puuid}";
            var res = await _http.GetFromJsonAsync<AccountResponse>(url);
            await Task.Delay(_options.RateLimitDelayMs);
            if (res == null) return null;
            return $"{res.gameName}#{res.tagLine}";
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("Riot account not found for PUUID: {PUUID}", puuid);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch RiotId for PUUID: {PUUID}", puuid);
            throw;
        }
    }
    
    public async Task<string?> GetMainRoleFromPuuidAsync(string puuid)
    {
        try
        {
            var url = $"lol/match/v5/matches/by-puuid/{puuid}/ids?queue=420&count=15";
            var matchIds = await _http.GetFromJsonAsync<List<string>>(url);
            if (matchIds == null || matchIds.Count == 0) return null;

            var roleCounts = new Dictionary<string, int>();

            foreach (var matchId in matchIds)
            {
                try
                {
                    var matchUrl = $"lol/match/v5/matches/{matchId}";
                    var match = await _http.GetFromJsonAsync<MatchResponse>(matchUrl);
                    await Task.Delay(_options.RateLimitDelayMs);
                    
                    var participant = match?.Info?.Participants?.FirstOrDefault(p => p.Puuid == puuid);
                    if (participant == null) continue;

                    var role = participant.TeamPosition;
                    if (string.IsNullOrWhiteSpace(role)) continue;

                    roleCounts[role] = roleCounts.GetValueOrDefault(role) + 1;
                }
                catch (HttpRequestException ex)
                {
                    _logger.LogWarning(ex, "Failed to fetch match {MatchId} for main role detection", matchId);
                }
            }

            return roleCounts.Count == 0 ? null : roleCounts.MaxBy(x => x.Value).Key;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch main role for PUUID: {PUUID}", puuid);
            return null;
        }
    }

    public async Task<RiotRankDto?> GetRankFromPuuidAsync(string puuid)
    {
        try
        {
            var url = $"{_options.RegionalBaseUrl}/lol/league/v4/entries/by-puuid/{puuid}";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("X-Riot-Token", _http.DefaultRequestHeaders.GetValues("X-Riot-Token").First());
            
            var res = await _http.SendAsync(req);
            res.EnsureSuccessStatusCode();
            
            var leagues = await res.Content.ReadFromJsonAsync<List<LeagueResponse>>();
            await Task.Delay(_options.RateLimitDelayMs);

            var soloQueue = leagues?.FirstOrDefault(x => x.QueueType == "RANKED_SOLO_5x5");
            if (soloQueue == null) return null;

            return new RiotRankDto 
            { 
                Tier = soloQueue.Tier, 
                Division = soloQueue.Rank, 
                LP = soloQueue.LeaguePoints 
            };
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            _logger.LogWarning("No ranked data found for PUUID: {PUUID}", puuid);
            return null;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Failed to fetch rank for PUUID: {PUUID}", puuid);
            throw;
        }
    }

    private static (string GameName, string TagLine) SplitRiotId(string riotId)
    {
        var parts = riotId.Split('#');
        if (parts.Length != 2) throw new ArgumentException("Format invalide, attendu: name#tag");
        return (parts[0], parts[1]);
    }
}