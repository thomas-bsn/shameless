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
            .Where(c => c.PlayerId == playerId)
            .OrderBy(c => c.Month)
            .ToListAsync();

    public async Task SaveAsync(PlayerMonthlyCost monthlyCost)
    {
        if (monthlyCost.Id == 0)
            _db.PlayerMonthlyCosts.Add(monthlyCost);
        await _db.SaveChangesAsync();
    }
}
