using Microsoft.EntityFrameworkCore;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Data.Repositories;

public sealed class PronosticsRepository : IPronosticsRepository
{
    private readonly AppDbContext _db;

    public PronosticsRepository(AppDbContext db) => _db = db;

    public Task<List<Pronostic>> GetDeLaGameAsync(int seriesId, int numeroGame) =>
        _db.Pronostics.AsNoTracking()
            .Include(p => p.Mvp)
            .Include(p => p.Dofd)
            .Where(p => p.SeriesId == seriesId && p.NumeroGame == numeroGame)
            .ToListAsync();

    public Task<Pronostic?> GetAsync(int seriesId, int numeroGame, long pronostiqueurDiscordId) =>
        _db.Pronostics.SingleOrDefaultAsync(p =>
            p.SeriesId == seriesId && p.NumeroGame == numeroGame && p.PronostiqueurDiscordId == pronostiqueurDiscordId);

    public Task<BestOfSeries?> GetSerieAsync(int seriesId) =>
        _db.BestOfSeries
            .AsNoTracking()
            .Include(s => s.Players).ThenInclude(p => p.Player).ThenInclude(p => p.User)
            .Include(s => s.Games)
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Id == seriesId);

    public void Add(Pronostic pronostic) => _db.Pronostics.Add(pronostic);

    public void Remove(Pronostic pronostic) => _db.Pronostics.Remove(pronostic);

    public Task<bool> TrySaveChangesAsync() => _db.TrySaveChangesAsync();
}
