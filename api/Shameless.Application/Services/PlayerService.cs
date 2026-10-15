using Shameless.Application.DTOs.Players;
using Shameless.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Shameless.Application.Services;

public class PlayerService : IPlayerService
{
    private readonly IPlayerRepository _players;
    private readonly IRiotService _riot;
    private readonly IUserRepository _users;
    private readonly ILogger<PlayerService> _logger;

    public PlayerService(
        IPlayerRepository players, 
        IRiotService riot, 
        IUserRepository users,
        ILogger<PlayerService> logger)
    {
        _players = players;
        _users = users;
        _riot = riot;
        _logger = logger;
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

        if (tier is "MASTER" or "GRANDMASTER" or "CHALLENGER")
            division = null;

        var player = new PlayerDto
        {
            Prenom = prenom,
            RiotId = riotId,
            Puuid = puuid,
            MainRole = mainRole,
            RankTier = tier,
            RankDivision = division,
            Lp = rank?.LP
        };

        var created = await _players.AddAsync(player);

        return new PlayerResponse
        {
            Id = created.Id,
            Prenom = created.Prenom,
            RiotId = created.RiotId,
            RankTier = created.RankTier,
            RankDivision = created.RankDivision,
            Lp = created.Lp
        };
    }
    
    public async Task RefreshRiotIdsAsync()
    {
        var players = await _players.GetAllAsync();

        foreach (var player in players)
        {
            if (string.IsNullOrEmpty(player.Puuid)) continue;

            try
            {
                var riotId = await _riot.GetRiotIdFromPuuidAsync(player.Puuid);

                if (riotId == null) continue;

                if (player.RiotId != riotId)
                    await _players.UpdateRiotIdAsync(player.Id, riotId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh RiotId for player {PlayerId} ({PUUID})", player.Id, player.Puuid);
            }
        }
    }
    
    public async Task<LinkResult> LinkPlayerAsync(string discordUserId, int playerId, string? username, string? avatarUrl, bool canTakeOver)
    {
        var discordIdLong = long.Parse(discordUserId);
        var user = await _users.GetByDiscordIdAsync(discordIdLong)
                   ?? await _users.CreateAsync(discordIdLong, username, avatarUrl);

        var player = await _players.GetByIdAsync(playerId)
                     ?? throw new InvalidOperationException("Player not found");

        if (player.UserId is { } ownerId && ownerId != user.Id && !canTakeOver)
            return LinkResult.AlreadyLinkedToAnotherUser;

        await _players.LinkUserAsync(playerId, user.Id);
        return LinkResult.Linked;
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

    public async Task<bool> UpdatePeakForPlayerAsync(int playerId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp)
    {
        if (await _players.GetByIdAsync(playerId) is null) return false;

        await _players.UpdatePeakAsync(playerId, peakTier, peakDivision, peakSeason, peakLp);
        return true;
    }

    public async Task<bool> SetCostAsync(int playerId, decimal cost)
    {
        var player = await _players.GetByIdAsync(playerId);
        if (player is null) return false;

        player.CurrentCost = cost;
        await _players.UpdateAsync(player);
        return true;
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
            if (string.IsNullOrWhiteSpace(player.Puuid)) continue;

            try
            {
                var rank = await _riot.GetRankFromPuuidAsync(player.Puuid);

                if (rank != null)
                {
                    var tier = rank.Tier?.ToUpper();
                    var division = (tier is "MASTER" or "GRANDMASTER" or "CHALLENGER")
                        ? null
                        : (int?)ParseDivision(rank.Division);
                    await _players.UpdateRankAsync(player.Id, tier, division, rank.LP);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh rank for player {PlayerId} ({PUUID})", player.Id, player.Puuid);
            }
        }
    }
    
    public async Task RefreshAllMainRolesAsync()
    {
        var players = await _players.GetAllAsync();

        foreach (var player in players)
        {
            if (string.IsNullOrWhiteSpace(player.Puuid)) continue;

            try
            {
                var mainRole = await _riot.GetMainRoleFromPuuidAsync(player.Puuid);
                await _players.UpdateMainRoleAsync(player.Id, mainRole);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to refresh main role for player {PlayerId} ({PUUID})", player.Id, player.Puuid);
            }
        }
    }

    private static int ParseDivision(string division) => division switch
    {
        "I" => 1, "II" => 2, "III" => 3, "IV" => 4, _ => 0
    };
}