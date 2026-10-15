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

    public async Task<List<PlayerMonthlyCost>> GetByMonthAsync(DateTime month)
    {
        var startOfMonth = new DateTime(month.Year, month.Month, 1);
        
        return await _db.PlayerMonthlyCosts
            .Where(c => c.Month == startOfMonth)
            .ToListAsync();
    }

    public async Task<PlayerMonthlyCost?> GetByPlayerAndMonthAsync(int playerId, DateTime month)
    {
        var startOfMonth = new DateTime(month.Year, month.Month, 1);
        
        return await _db.PlayerMonthlyCosts
            .FirstOrDefaultAsync(c => c.PlayerId == playerId && c.Month == startOfMonth);
    }

    public async Task AddAsync(PlayerMonthlyCost monthlyCost)
    {
        _db.PlayerMonthlyCosts.Add(monthlyCost);
        await _db.SaveChangesAsync();
    }

    public async Task AddRangeAsync(List<PlayerMonthlyCost> monthlyCosts)
    {
        _db.PlayerMonthlyCosts.AddRange(monthlyCosts);
        await _db.SaveChangesAsync();
    }
}