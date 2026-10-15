using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Repositories;

public interface IPlayerMonthlyCostRepository
{
    Task<List<PlayerMonthlyCost>> GetByMonthAsync(DateTime month);
    Task<PlayerMonthlyCost?> GetByPlayerAndMonthAsync(int playerId, DateTime month);
    Task AddAsync(PlayerMonthlyCost monthlyCost);
    Task AddRangeAsync(List<PlayerMonthlyCost> monthlyCosts);
}