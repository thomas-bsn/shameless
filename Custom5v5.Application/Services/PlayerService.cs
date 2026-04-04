using Custom5v5.Application.DTOs.Players;
using Custom5v5.Application.Interfaces;

namespace Custom5v5.Api.Services;

public class PlayerService : IPlayerService
{
    private readonly IPlayerRepository _players;
    private readonly IRiotService _riot;
    private readonly IUserRepository _users;

    public PlayerService(IPlayerRepository players, IRiotService riot, IUserRepository users)
    {
        _players = players;
        _users = users;
        _riot = riot;
    }

    public Task<List<PlayerDto>> GetPlayersAsync() =>
        _players.GetAllAsync();

    public async Task<PlayerResponse> CreatePlayerAsync(string prenom, string riotId)
    {
        if (string.IsNullOrWhiteSpace(prenom) || string.IsNullOrWhiteSpace(riotId))
            throw new ArgumentException("Invalid player data");
        var puuid = await _riot.GetPuuidFromRiotIdAsync(riotId)
                    ?? throw new InvalidOperationException("Riot account not found");
        
        if (await _players.ExistsByPuuidAsync(puuid))
            throw new InvalidOperationException("Player already exists");

        var rank = await _riot.GetRankFromPuuidAsync(puuid);
        var mainRole = await _riot.GetMainRoleFromPuuidAsync(puuid);

        var tier = rank?.Tier?.ToUpper();
        int? division = rank != null ? ParseDivision(rank.Division) : null;

        // Master+ n'a pas de division
        if (tier is "MASTER" or "GRANDMASTER" or "CHALLENGER")
            division = null;

        var player = new PlayerDto
        {
            Prenom = prenom,
            RiotId = riotId,
            PUUID = puuid,
            MainRole = mainRole,
            RankTier = tier,
            RankDivision = division,
            LP = rank?.LP
        };

        var created = await _players.AddAsync(player);

        return new PlayerResponse
        {
            Id = created.Id,
            Prenom = created.Prenom,
            RiotId = created.RiotId,
            RankTier = created.RankTier,
            RankDivision = created.RankDivision,
            LP = created.LP
        };
    }
    
    public async Task LinkPlayerAsync(string discordUserId, int playerId, string? username, string? avatarUrl)
    {
        var discordIdLong = long.Parse(discordUserId);
        var user = await _users.GetByDiscordIdAsync(discordIdLong)
                   ?? await _users.CreateAsync(discordIdLong, username, avatarUrl);

        await _players.LinkUserAsync(playerId, user.Id);
    }
    
    public async Task LinkPlayerAsync(string discordUserId, int playerId)
    {
        var discordIdLong = long.Parse(discordUserId);
        var user = await _users.GetByDiscordIdAsync(discordIdLong)
                   ?? await _users.CreateAsync(discordIdLong, null, null);

        await _players.LinkUserAsync(playerId, user.Id);
    }
    
    public async Task UpdatePeakAsync(string discordUserId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp)
    {
        var discordIdLong = long.Parse(discordUserId);
        var user = await _users.GetByDiscordIdAsync(discordIdLong);
        if (user == null) throw new Exception("User not found");

        var player = await _players.GetByUserIdAsync(user.Id);
        if (player == null) throw new Exception("Player not found");

        await _players.UpdatePeakAsync(player.Id, peakTier, peakDivision, peakSeason, peakLp);
    }

    public async Task<PlayerDto?> GetPlayerByDiscordIdAsync(string discordUserId)
    {
        var discordIdLong = long.Parse(discordUserId);
        var user = await _users.GetByDiscordIdAsync(discordIdLong);
        if (user == null) return null;

        return await _players.GetByUserIdAsync(user.Id);
    }
    
    public async Task RefreshAllRanksAsync()
    {
        var players = await _players.GetAllAsync();

        foreach (var player in players)
        {
            if (string.IsNullOrWhiteSpace(player.PUUID)) continue;

            var rank = await _riot.GetRankFromPuuidAsync(player.PUUID);
            await Task.Delay(1200);

            if (rank != null)
            {
                var tier = rank.Tier?.ToUpper();
                var division = (tier is "MASTER" or "GRANDMASTER" or "CHALLENGER")
                    ? null
                    : (int?)ParseDivision(rank.Division);
                await _players.UpdateRankAsync(player.Id, tier, division, rank.LP);
            }
        }
    }
    
    public async Task RefreshAllMainRolesAsync()
    {
        var players = await _players.GetAllAsync();

        foreach (var player in players)
        {
            if (string.IsNullOrWhiteSpace(player.PUUID)) continue;

            var mainRole = await _riot.GetMainRoleFromPuuidAsync(player.PUUID);
            await _players.UpdateMainRoleAsync(player.Id, mainRole);
        }
    }

    private static int ParseDivision(string division) => division switch
    {
        "I" => 1, "II" => 2, "III" => 3, "IV" => 4, _ => 0
    };
}