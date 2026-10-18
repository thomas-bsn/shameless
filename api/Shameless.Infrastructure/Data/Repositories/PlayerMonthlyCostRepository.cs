using Shameless.Application.Entities;
using Shameless.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Shameless.Infrastructure.Repositories;

public class PlayerMonthlyCostRepository : IPlayerMonthlyCostRepository
{
    private readonly AppDbContext _db;

    public PlayerMonthlyCostRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<PlayerMonthlyCost>> GetByPlayerAsync(int playerId) =>
        await _db.PlayerMonthlyCosts
            .AsNoTracking()
            .Where(c => c.PlayerId == playerId)
            .OrderBy(c => c.Month)
            .ToListAsync();

    public async Task<Dictionary<int, decimal>> GetCostsFinauxAvantAsync(DateOnly mois)
    {
        var limite = mois.ToDateTime(TimeOnly.MinValue);
        var lignes = await _db.PlayerMonthlyCosts
            .AsNoTracking()
            .Where(c => c.Month < limite)
            .Select(c => new { c.PlayerId, c.Month, c.FinalCost })
            .ToListAsync();
        return lignes
            .GroupBy(c => c.PlayerId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.Month).First().FinalCost);
    }

    public async Task UpsertAsync(PlayerMonthlyCost monthlyCost)
    {
        var mois = DateOnly.FromDateTime(monthlyCost.Month);
        var source = monthlyCost.SourceDuCostDeDepart?.ToString();
        // Unique (player_id, month) : deux games terminées en même temps écrivent la même ligne.
        await _db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO player_monthly_costs
                (player_id, month, final_cost, cost_de_depart, source_du_cost_de_depart, note_moyenne,
                 base_mmr, winrate_adjustment, games_played, wins)
            VALUES
                ({monthlyCost.PlayerId}, {mois}, {monthlyCost.FinalCost}, {monthlyCost.CostDeDepart}, {source},
                 {monthlyCost.NoteMoyenne}, {monthlyCost.BaseMMR}, {monthlyCost.WinrateAdjustment},
                 {monthlyCost.GamesPlayed}, {monthlyCost.Wins})
            ON CONFLICT (player_id, month) DO UPDATE SET
                final_cost = EXCLUDED.final_cost,
                cost_de_depart = EXCLUDED.cost_de_depart,
                source_du_cost_de_depart = EXCLUDED.source_du_cost_de_depart,
                note_moyenne = EXCLUDED.note_moyenne,
                games_played = EXCLUDED.games_played,
                wins = EXCLUDED.wins
            """);
    }
}