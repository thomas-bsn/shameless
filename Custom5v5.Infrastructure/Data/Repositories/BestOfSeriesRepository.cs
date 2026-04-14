using Custom5v5.Application.Entities;
using Custom5v5.Application.Interfaces;
namespace Custom5v5.Infrastructure.Data.Repositories;

public class BestOfSeriesRepository : IBestOfSeriesRepository
{
    private readonly AppDbContext _db;

    public BestOfSeriesRepository(AppDbContext db) => _db = db;

    public async Task<BestOfSeries> AddAsync(BestOfSeries series)
    {
        _db.BestOfSeries.Add(series);
        await _db.SaveChangesAsync();
        return series;
    }
    
    public async Task DeleteAsync(int seriesId)
    {
        var series = await _db.BestOfSeries.FindAsync(seriesId);
        if (series == null) return;
        _db.BestOfSeries.Remove(series);
        await _db.SaveChangesAsync();
    }

    public async Task<BestOfSeries?> GetByIdAsync(int id) =>
        await _db.BestOfSeries.FindAsync(id);
}