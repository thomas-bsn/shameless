using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

public interface IBestOfSeriesRepository
{
    Task<BestOfSeries> AddAsync(BestOfSeries series);
    Task<BestOfSeries?> GetByIdAsync(int id);
    Task DeleteAsync(int seriesId);
    Task SaveChangesAsync();
}