using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Data.Repositories;

public class SoireeRepository : ISoireeRepository
{
    private readonly AppDbContext _db;

    public SoireeRepository(AppDbContext db) => _db = db;

    public Task<Soiree?> GetOpenAsync() =>
        _db.Soirees.SingleOrDefaultAsync(s => s.Status == SoireeStatus.Open);

    public async Task<Soiree?> GetByIdAsync(int id) =>
        await _db.Soirees.FindAsync(id);

    public Task<Soiree?> GetOpenWithSeriesAndGamesAsync() =>
        _db.Soirees
            .Include(s => s.Series)
            .ThenInclude(s => s.Games)
            .AsSplitQuery()
            .SingleOrDefaultAsync(s => s.Status == SoireeStatus.Open);

    public async Task<bool> TryAddAsync(Soiree soiree)
    {
        _db.Soirees.Add(soiree);
        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _db.Entry(soiree).State = EntityState.Detached;
            return false;
        }
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
