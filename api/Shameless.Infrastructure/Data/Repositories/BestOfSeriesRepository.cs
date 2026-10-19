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

    public Task<BestOfSeries?> GetWithLiensAsync(int id) =>
        _db.BestOfSeries.Include(s => s.Liens).Include(s => s.Players).SingleOrDefaultAsync(s => s.Id == id);

    public Task<LienAcces?> GetLienAsync(string secret) =>
        _db.LiensAcces.Include(l => l.Series).SingleOrDefaultAsync(l => l.Secret == secret);

    public Task<BestOfSeries?> GetByCodeCompanionAsync(string code) =>
        _db.BestOfSeries.Include(s => s.Players).ThenInclude(p => p.Player).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(s => s.CodeCompanion == code);

    /// <summary>
    /// Les Séries avec leur composition courante, leurs liens et leurs games (participants et joueurs,
    /// Draft de champions, rapports du Companion mis en revue), games triées par id.
    /// </summary>
    private IQueryable<BestOfSeries> WithGames() =>
        _db.BestOfSeries
            .Include(s => s.Players).ThenInclude(p => p.Player).ThenInclude(p => p.User)
            .Include(s => s.Liens)
            .Include(s => s.Games.OrderBy(g => g.Id)).ThenInclude(g => g.Participants).ThenInclude(p => p.Player).ThenInclude(p => p.User)
            .Include(s => s.Games.OrderBy(g => g.Id)).ThenInclude(g => g.DraftChampions).ThenInclude(d => d!.Actions)
            .Include(s => s.Games.OrderBy(g => g.Id)).ThenInclude(g => g.Rapports)
            .AsSplitQuery();
}
