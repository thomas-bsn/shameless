using Shameless.Application.DTOs.Players;
using Shameless.Application.DTOs.Riot;
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

    public Task<PlayerDto?> GetPlayerAsync(int playerId) =>
        _players.GetDtoByIdAsync(playerId);

    public async Task<RiotAccountPreview?> PreviewRiotAccountAsync(string riotId)
    {
        if (string.IsNullOrWhiteSpace(riotId)) return null;

        var puuid = await _riot.GetPuuidFromRiotIdAsync(riotId);
        if (puuid == null) return null;

        var rank = (await _riot.GetRanksFromPuuidAsync(puuid)).Solo;
        var (tier, division) = NormalizeRank(rank);

        return new RiotAccountPreview(
            RiotId: riotId,
            RankTier: tier,
            RankDivision: division,
            Lp: rank?.LP,
            MainRole: await _riot.GetMainRoleFromPuuidAsync(puuid),
            AlreadyRegistered: await _players.ExistsByPuuidAsync(puuid));
    }

    public async Task<PlayerResponse> CreatePlayerAsync(string prenom, string riotId)
    {
        if (string.IsNullOrWhiteSpace(prenom) || string.IsNullOrWhiteSpace(riotId))
            throw new ArgumentException("Invalid player data");
        var puuid = await _riot.GetPuuidFromRiotIdAsync(riotId)
                    ?? throw new InvalidOperationException("Riot account not found");
        
        if (await _players.ExistsByPuuidAsync(puuid))
            throw new InvalidOperationException("Player already exists");

        var ranks = await _riot.GetRanksFromPuuidAsync(puuid);
        var rank = ranks.Solo;
        var mainRole = await _riot.GetMainRoleFromPuuidAsync(puuid);
        var (tier, division) = NormalizeRank(rank);
        var (flexTier, flexDivision) = NormalizeRank(ranks.Flex);

        var player = new PlayerDto
        {
            Prenom = prenom,
            RiotId = riotId,
            Puuid = puuid,
            MainRole = mainRole,
            RankTier = tier,
            RankDivision = division,
            Lp = rank?.LP,
            FlexRankTier = flexTier,
            FlexRankDivision = flexDivision,
            FlexLp = ranks.Flex?.LP
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
        var player = await _players.GetByIdAsync(playerId);
        if (player is null) return LinkResult.PlayerNotFound;

        var discordIdLong = long.Parse(discordUserId);
        var user = await _users.GetByDiscordIdAsync(discordIdLong);

        if (player.UserId is { } ownerId && ownerId != user?.Id && !canTakeOver)
            return LinkResult.AlreadyLinkedToAnotherUser;

        user ??= await _users.CreateAsync(discordIdLong, username, avatarUrl);

        await _players.LinkUserAsync(playerId, user.Id);
        return LinkResult.Linked;
    }
    
    public async Task<bool> UpdatePeakAsync(string discordUserId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp)
    {
        var player = await GetPlayerByDiscordIdAsync(discordUserId);
        if (player == null) return false;

        await _players.UpdatePeakAsync(player.Id, peakTier, peakDivision, peakSeason, peakLp);
        return true;
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
                var ranks = await _riot.GetRanksFromPuuidAsync(player.Puuid);

                // Une file absente de la réponse garde le dernier rang connu.
                if (ranks.Solo is { } solo)
                {
                    var (tier, division) = NormalizeRank(solo);
                    await _players.UpdateRankAsync(player.Id, tier, division, solo.LP);
                }
                if (ranks.Flex is { } flex)
                {
                    var (tier, division) = NormalizeRank(flex);
                    await _players.UpdateFlexRankAsync(player.Id, tier, division, flex.LP);
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

    /// <summary>Tier en majuscules ; pas de division à partir de Master.</summary>
    private static (string? Tier, int? Division) NormalizeRank(RiotRankDto? rank)
    {
        var tier = rank?.Tier?.ToUpper();
        int? division = rank != null && tier is not ("MASTER" or "GRANDMASTER" or "CHALLENGER")
            ? ParseDivision(rank.Division)
            : null;
        return (tier, division);
    }

    private static int ParseDivision(string division) => division switch
    {
        "I" => 1, "II" => 2, "III" => 3, "IV" => 4, _ => 0
    };
}