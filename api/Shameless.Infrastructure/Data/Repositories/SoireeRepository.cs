using Microsoft.EntityFrameworkCore;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Data.Repositories;

public class SoireeRepository : ISoireeRepository
{
    private readonly AppDbContext _db;

    public SoireeRepository(AppDbContext db) => _db = db;

    public Task<Soiree?> GetOpenAsync() =>
        _db.Soirees.SingleOrDefaultAsync(s => s.Status == SoireeStatus.Open);

    public Task<Soiree?> GetDerniereFermeeAsync() =>
        _db.Soirees.Where(s => s.Status == SoireeStatus.Closed && s.FermetureAutomatique)
            .OrderByDescending(s => s.ClosedAt)
            .Include(s => s.Picker)
            .FirstOrDefaultAsync();

    public async Task<Soiree?> GetByIdAsync(int id) =>
        await _db.Soirees.FindAsync(id);

    public Task<Soiree?> GetWithSeriesAsync(int id) =>
        _db.Soirees.Include(s => s.Series).SingleOrDefaultAsync(s => s.Id == id);

    public Task<Soiree?> GetOpenWithSeriesAndGamesAsync() =>
        _db.Soirees
            .Include(s => s.Series)
            .ThenInclude(s => s.Games)
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Status == SoireeStatus.Open);

    public Task<bool> TryAddAsync(Soiree soiree) => _db.TryAddAsync(soiree);

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
