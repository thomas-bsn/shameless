using System.Net.Http.Json;
using Custom5v5.Application.DTOs.Riot;
using Custom5v5.Application.Interfaces;

public class RiotService : IRiotService
{
    private readonly HttpClient _http;

    public RiotService(HttpClient http) => _http = http;

    // Point d'entrée principal : riotId → rank
    public async Task<RiotRankDto?> GetRankAsync(string riotId)
    {
        var puuid = await GetPuuidFromRiotIdAsync(riotId);
        if (puuid == null) return null;

        return await GetRankFromPuuidAsync(puuid);
    }

    // Exposé publiquement : riotId → puuid
    public async Task<string?> GetPuuidFromRiotIdAsync(string riotId)
    {
        var (gameName, tagLine) = SplitRiotId(riotId);
        return await GetPuuidAsync(gameName, tagLine);
    }

    // Bas niveau : gameName + tag → puuid
    private async Task<string?> GetPuuidAsync(string gameName, string tagLine)
    {
        var url = $"riot/account/v1/accounts/by-riot-id/{gameName}/{tagLine}";
        var res = await _http.GetFromJsonAsync<AccountResponse>(url);
        return res?.puuid;
    }
    
    public async Task<string?> GetRiotIdFromPuuidAsync(string puuid)
    {
        var url = $"riot/account/v1/accounts/by-puuid/{puuid}";
        var res = await _http.GetFromJsonAsync<AccountResponse>(url);
        if (res == null) return null;
        return $"{res.gameName}#{res.tagLine}";
    }
    
    public async Task<string?> GetMainRoleFromPuuidAsync(string puuid)
    {
        var url = $"lol/match/v5/matches/by-puuid/{puuid}/ids?queue=420&count=15";
        var matchIds = await _http.GetFromJsonAsync<List<string>>(url);
        if (matchIds == null || matchIds.Count == 0) return null;

        var roleCounts = new Dictionary<string, int>();

        foreach (var matchId in matchIds)
        {
            var matchUrl = $"lol/match/v5/matches/{matchId}";
            var match = await _http.GetFromJsonAsync<MatchResponse>(matchUrl);
            await Task.Delay(1200);
            var participant = match?.Info?.Participants?.FirstOrDefault(p => p.Puuid == puuid);
            if (participant == null) continue;

            var role = participant.TeamPosition; // "TOP", "JUNGLE", "MIDDLE", "BOTTOM", "UTILITY"
            if (string.IsNullOrWhiteSpace(role)) continue;

            roleCounts[role] = roleCounts.GetValueOrDefault(role) + 1;
        }

        return roleCounts.Count == 0 ? null : roleCounts.MaxBy(x => x.Value).Key;
    }

    // Bas niveau : puuid → rank
    public async Task<RiotRankDto?> GetRankFromPuuidAsync(string puuid)
    {
        // euw1 est un domaine différent, il faut une URL absolue ici
        var url = $"https://euw1.api.riotgames.com/lol/league/v4/entries/by-puuid/{puuid}";
        var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("X-Riot-Token", _http.DefaultRequestHeaders.GetValues("X-Riot-Token").First());
        var res = await _http.SendAsync(req);
        res.EnsureSuccessStatusCode();
        var leagues = await res.Content.ReadFromJsonAsync<List<LeagueResponse>>();

        var soloQueue = leagues?.FirstOrDefault(x => x.QueueType == "RANKED_SOLO_5x5");
        if (soloQueue == null) return null;

        return new RiotRankDto { Tier = soloQueue.Tier, Division = soloQueue.Rank, LP = soloQueue.LeaguePoints };
    }

    private static (string GameName, string TagLine) SplitRiotId(string riotId)
    {
        var parts = riotId.Split('#');
        if (parts.Length != 2) throw new ArgumentException("Format invalide, attendu: name#tag");
        return (parts[0], parts[1]);
    }
    
    public record MatchResponse(MatchInfo? Info);
    public record MatchInfo(List<MatchParticipant>? Participants);
    public record MatchParticipant(string Puuid, string TeamPosition);
}