using Microsoft.EntityFrameworkCore;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Data.Repositories;

public class BestOfSeriesRepository : IBestOfSeriesRepository
{
    private readonly AppDbContext _db;

    public BestOfSeriesRepository(AppDbContext db) => _db = db;

    public void Add(BestOfSeries series) => _db.BestOfSeries.Add(series);

    public Task<bool> TrySaveChangesAsync() => _db.TrySaveChangesAsync();

    public void Remove(BestOfSeries series) => _db.BestOfSeries.Remove(series);

    public async Task<BestOfSeries?> GetByIdAsync(int id) =>
        await _db.BestOfSeries.FindAsync(id);

    public Task<BestOfSeries?> GetWithGamesAsync(int id) =>
        WithGames().SingleOrDefaultAsync(s => s.Id == id);

    public Task<List<BestOfSeries>> GetEnCoursWithGamesAsync() =>
        WithGames().Where(s => s.Outcome == SeriesOutcome.EnCours).OrderBy(s => s.Id).ToListAsync();

    public Task<List<BestOfSeries>> GetBySoireeWithGamesAsync(int soireeId) =>
        WithGames().Where(s => s.SoireeId == soireeId).OrderBy(s => s.Id).ToListAsync();

    /// <summary>Les Séries avec leur composition courante et leurs games (participants et joueurs), games triées par id.</summary>
    private IQueryable<BestOfSeries> WithGames() =>
        _db.BestOfSeries
            .Include(s => s.Players).ThenInclude(p => p.Player)
            .Include(s => s.Games.OrderBy(g => g.Id)).ThenInclude(g => g.Participants).ThenInclude(p => p.Player)
            .AsSplitQuery();
}
