using Custom5v5.Application.Interfaces;
using Custom5v5.Infrastructure.Repositories;

namespace Custom5v5.Application.Services;

public class CostCalculationService
{
    private readonly ICustomGameRepository _gameRepo;
    private readonly IPlayerRepository _playerRepo; // ✅ Utilise ton interface existante
    private readonly IPlayerMonthlyCostRepository _monthlyCostRepo;

    public CostCalculationService(
        ICustomGameRepository gameRepo,
        IPlayerRepository playerRepo,
        IPlayerMonthlyCostRepository monthlyCostRepo)
    {
        _gameRepo = gameRepo;
        _playerRepo = playerRepo;
        _monthlyCostRepo = monthlyCostRepo;
    }

    public async Task RecalculateCosts(int gameId)
    {
        var game = await _gameRepo.GetByIdWithParticipantsAsync(gameId);
        if (game == null) return;
    
        var currentMonth = new DateTime(game.PlayedAt.Year, game.PlayedAt.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    
        foreach (var participant in game.Participants)
        {
            await RecalculatePlayerCost(participant.PlayerId, currentMonth);
        }
    }

    public async Task RecalculatePlayerCost(int playerId, DateTime month)
    {
        var player = await _playerRepo.GetByIdAsync(playerId);
        if (player == null) return;
    
        // ✅ Passer un DateTime UTC
        var utcMonth = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var monthGames = await _gameRepo.GetPlayerGamesForMonthAsync(playerId, utcMonth);
    
        var gamesPlayed = monthGames.Count;
        var wins = monthGames.Count(g => g.Won);
    
        var finalMMR = MMRCalculationService.CalculateFinalMMR(
            player.PeakTier,
            player.PeakDivision,
            gamesPlayed,
            wins
        );
    
        player.CurrentCost = MMRCalculationService.MMRToCost(finalMMR);
    
        if (gamesPlayed >= 3)
        {
            player.IsPlacement = false;
        }
    
        await _playerRepo.UpdateAsync(player);
    }

    public async Task InitializeNewMonth()
    {
        var lastMonth = DateTime.UtcNow.AddMonths(-1);
        var lastMonthStart = new DateTime(lastMonth.Year, lastMonth.Month, 1);
        
        var lastMonthlyCosts = await _monthlyCostRepo.GetByMonthAsync(lastMonthStart);
        var players = await _playerRepo.GetAllEntitiesAsync(); // ✅ Nouvelle méthode
        
        foreach (var player in players)
        {
            var lastCost = lastMonthlyCosts.FirstOrDefault(c => c.PlayerId == player.Id);
            
            if (lastCost != null)
            {
                player.CurrentCost = lastCost.FinalCost;
            }
            else
            {
                var soloQMMR = MMRCalculationService.GetPeakRankMMR(
                    player.PeakTier,
                    player.PeakDivision
                );
                player.CurrentCost = MMRCalculationService.MMRToCost(soloQMMR);
            }
            
            player.IsPlacement = true;
        }
        
        await _playerRepo.UpdateRangeAsync(players); // ✅ Nouvelle méthode
    }

    public async Task<(string name, decimal cost, int mmr, int baseMMR, int winrateAdjustment)> CalculatePlayerCostPreview(
        int playerId,
        DateTime? month = null)
    {
        var player = await _playerRepo.GetByIdAsync(playerId); // ✅ Nouvelle méthode
        if (player == null)
            throw new ArgumentException($"Player {playerId} not found");
        
        var targetMonth = month ?? DateTime.UtcNow;
        var monthGames = await _gameRepo.GetPlayerGamesForMonthAsync(playerId, targetMonth);
        
        var gamesPlayed = monthGames.Count;
        var wins = monthGames.Count(g => g.Won);
        
        var baseMMR = MMRCalculationService.GetPeakRankMMR(player.PeakTier, player.PeakDivision);
        var winrateAdjustment = MMRCalculationService.CalculateWinrateAdjustment(gamesPlayed, wins);
        var finalMMR = baseMMR + winrateAdjustment;
        var cost = MMRCalculationService.MMRToCost(finalMMR);

        return (player.Prenom,cost, finalMMR, baseMMR, winrateAdjustment);
    }
}