using Shameless.Application.DTOs.Players;
using Shameless.Application.Cost;
using Shameless.Application.DTOs.Riot;
using Shameless.Application.Interfaces;
using Shameless.Application.Riot;
using Shameless.Application.Joueurs;
using Microsoft.Extensions.Logging;

namespace Shameless.Application.Services;

public class PlayerService : IPlayerService
{
    private readonly IPlayerRepository _players;
    private readonly IRiotService _riot;
    private readonly IUserRepository _users;
    private readonly ILogger<PlayerService> _logger;
    private readonly IPlayerMonthlyCostRepository _monthlyCosts;
    private readonly TimeProvider _clock;

    public PlayerService(
        IPlayerRepository players, 
        IPlayerMonthlyCostRepository monthlyCosts,
        TimeProvider clock,
        IRiotService riot, 
        IUserRepository users,
        ILogger<PlayerService> logger)
    {
        _players = players;
        _monthlyCosts = monthlyCosts;
        _clock = clock;
        _users = users;
        _riot = riot;
        _logger = logger;
    }

    public async Task<List<PlayerDto>> GetPlayersAsync()
    {
        var players = await _players.GetAllAsync();
        var maintenant = _clock.GetUtcNow().UtcDateTime;
        var precedents = await _monthlyCosts.GetCostsFinauxDuMoisAsync(new DateOnly(maintenant.Year, maintenant.Month, 1).AddMonths(-1));
        foreach (var player in players)
            player.CostEvolution = precedents.TryGetValue(player.Id, out var avant) ? player.CurrentCost - avant : null;
        return players;
    }

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
            MainRole: await MainRoleOrNullAsync(puuid),
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
        var mainRole = await MainRoleOrNullAsync(puuid);
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
            FlexLp = ranks.Flex?.LP,
            // Sans historique de cost, le Rang SoloQ donne le cost initial (ADR 0012).
            CurrentCost = CostDeDepart.DuRangSoloQ(tier, division).Cost
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
    
    public async Task<bool> UnlinkPlayerAsync(string discordUserId)
    {
        var user = await _users.GetByDiscordIdAsync(long.Parse(discordUserId));
        return user is not null && await _players.UnlinkUserAsync(user.Id);
    }

    public async Task<PeakResult> UpdatePeakAsync(string discordUserId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp)
    {
        if (!SaisonValide(peakTier, peakSeason)) return PeakResult.InvalidSeason;

        var player = await GetPlayerByDiscordIdAsync(discordUserId);
        if (player == null) return PeakResult.PlayerNotFound;

        await _players.UpdatePeakAsync(player.Id, peakTier, peakDivision, peakSeason, peakLp);
        return PeakResult.Updated;
    }

    public async Task<PeakResult> UpdatePeakForPlayerAsync(int playerId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp)
    {
        if (!SaisonValide(peakTier, peakSeason)) return PeakResult.InvalidSeason;
        if (await _players.GetByIdAsync(playerId) is null) return PeakResult.PlayerNotFound;

        await _players.UpdatePeakAsync(playerId, peakTier, peakDivision, peakSeason, peakLp);
        return PeakResult.Updated;
    }

    /// <summary>Un peak porte toujours sa saison, de S24 à la saison courante ; l'effacer (sans tier) n'en demande pas.</summary>
    private bool SaisonValide(string? peakTier, string? peakSeason) =>
        peakTier is null || SaisonsDePeak.EstValide(peakSeason, _clock.GetUtcNow().Year);

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
            await TryRefreshRanksAsync(player);
        }
    }

    public async Task RefreshAllMainRolesAsync()
    {
        var players = await _players.GetAllAsync();

        foreach (var player in players)
        {
            if (string.IsNullOrWhiteSpace(player.Puuid)) continue;
            await TryRefreshMainRoleAsync(player);
        }
    }

    public async Task<ResultatActualisationJoueur?> RefreshRiotAsync(int playerId)
    {
        var player = await _players.GetDtoByIdAsync(playerId);
        if (player is null) return null;

        if (string.IsNullOrWhiteSpace(player.Puuid))
            return new ResultatActualisationJoueur(player.Id, player.Prenom, false, false, ["Aucun compte Riot connu pour ce joueur."]);

        var echecs = new List<string>();
        var rangs = await TryRefreshRanksAsync(player);
        if (rangs is not null) echecs.Add(rangs);
        var role = await TryRefreshMainRoleAsync(player);
        if (role is not null) echecs.Add(role);
        return new ResultatActualisationJoueur(player.Id, player.Prenom, rangs is null, role is null, echecs);
    }

    /// <summary>Relit les rangs SoloQ et Flex ; renvoie le message d'échec, ou null si tout s'est bien passé.</summary>
    private async Task<string?> TryRefreshRanksAsync(PlayerDto player)
    {
        try
        {
            var ranks = await _riot.GetRanksFromPuuidAsync(player.Puuid!);

            // Riot a répondu : une file absente de la réponse, c'est un joueur non classé dans
            // cette file. Si l'appel échoue, les derniers rangs connus restent.
            var (soloTier, soloDivision) = NormalizeRank(ranks.Solo);
            await _players.UpdateRankAsync(player.Id, soloTier, soloDivision, ranks.Solo?.LP);
            var (flexTier, flexDivision) = NormalizeRank(ranks.Flex);
            await _players.UpdateFlexRankAsync(player.Id, flexTier, flexDivision, ranks.Flex?.LP);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh rank for player {PlayerId} ({PUUID})", player.Id, player.Puuid);
            return "Rangs : Riot n'a pas répondu, les anciens rangs sont conservés.";
        }
    }

    /// <summary>
    /// Recalcule le rôle principal ; renvoie le message d'échec, ou null. Sur un échec de Riot ou sans
    /// partie lisible, l'ancien rôle est conservé (jamais écrasé par null).
    /// </summary>
    private async Task<string?> TryRefreshMainRoleAsync(PlayerDto player)
    {
        try
        {
            var mainRole = await _riot.GetMainRoleFromPuuidAsync(player.Puuid!);
            if (mainRole is not null) await _players.UpdateMainRoleAsync(player.Id, mainRole);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh main role for player {PlayerId} ({PUUID})", player.Id, player.Puuid);
            return "Rôle principal : Riot n'a pas répondu, l'ancien rôle est conservé.";
        }
    }

    /// <summary>Le rôle principal d'un compte pas encore enregistré : null si Riot échoue.</summary>
    private async Task<string?> MainRoleOrNullAsync(string puuid)
    {
        try { return await _riot.GetMainRoleFromPuuidAsync(puuid); }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Main role unavailable for PUUID {PUUID}", puuid);
            return null;
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