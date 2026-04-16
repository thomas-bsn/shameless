using Custom5v5.Application.Entities;

namespace Custom5v5.Application.Interfaces;

public interface IBestOfSeriesRepository
{
    Task<BestOfSeries> AddAsync(BestOfSeries series);
    Task<BestOfSeries?> GetByIdAsync(int id);
    Task DeleteAsync(int seriesId);
    Task SaveChangesAsync();
}