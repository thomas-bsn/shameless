using Microsoft.EntityFrameworkCore;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Data.Repositories;

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

    public async Task SaveChangesAsync()
    {
        await _db.SaveChangesAsync();
    }

    public void Remove(BestOfSeries series) => _db.BestOfSeries.Remove(series);

    public async Task<BestOfSeries?> GetByIdAsync(int id) =>
        await _db.BestOfSeries.FindAsync(id);

    public Task<BestOfSeries?> GetWithGamesAsync(int id) =>
        _db.BestOfSeries
            .Include(s => s.Players).ThenInclude(p => p.Player)
            .Include(s => s.Games.OrderBy(g => g.Id)).ThenInclude(g => g.Participants).ThenInclude(p => p.Player)
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Id == id);

    public Task<List<int>> GetEnCoursIdsAsync() =>
        _db.BestOfSeries
            .Where(s => s.Outcome == SeriesOutcome.EnCours)
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .ToListAsync();

    public Task<List<int>> GetIdsBySoireeAsync(int soireeId) =>
        _db.BestOfSeries
            .Where(s => s.SoireeId == soireeId)
            .OrderBy(s => s.Id)
            .Select(s => s.Id)
            .ToListAsync();
}
