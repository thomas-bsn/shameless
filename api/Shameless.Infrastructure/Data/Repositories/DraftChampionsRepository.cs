using Microsoft.EntityFrameworkCore;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Data.Repositories;

public class DraftChampionsRepository : IDraftChampionsRepository
{
    private readonly AppDbContext _db;

    public DraftChampionsRepository(AppDbContext db) => _db = db;

    public void Add(DraftChampions draft) => _db.DraftsChampions.Add(draft);

    public Task<DraftChampions?> GetByGameAsync(int gameId) =>
        Complete().SingleOrDefaultAsync(d => d.GameId == gameId);

    public Task<DraftChampions?> GetDerniereOfSeriesAsync(int seriesId) =>
        Complete().Where(d => d.Game.SeriesId == seriesId).OrderByDescending(d => d.GameId).FirstOrDefaultAsync();

    public Task<bool> TrySaveChangesAsync() => _db.TrySaveChangesAsync();

    private IQueryable<DraftChampions> Complete() =>
        _db.DraftsChampions
            .Include(d => d.Actions.OrderBy(a => a.Numero))
            .Include(d => d.Game).ThenInclude(g => g.Participants).ThenInclude(p => p.Player).ThenInclude(p => p.User)
            .Include(d => d.Game).ThenInclude(g => g.Series).ThenInclude(s => s!.Players)
            .Include(d => d.Game).ThenInclude(g => g.Series).ThenInclude(s => s!.Games).ThenInclude(g => g.Participants)
            .AsSplitQuery();
}
