using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Data.Repositories;

public class PickerSessionRepository : IPickerSessionRepository
{
    private readonly AppDbContext _db;

    public PickerSessionRepository(AppDbContext db) => _db = db;

    public Task<PickerSession?> GetBySoireeAsync(int soireeId) =>
        _db.PickerSessions
            .Include(s => s.Players.OrderBy(p => p.Position)).ThenInclude(p => p.Player)
            .SingleOrDefaultAsync(s => s.SoireeId == soireeId);

    public async Task<bool> TryAddAsync(PickerSession session)
    {
        _db.PickerSessions.Add(session);
        try
        {
            await _db.SaveChangesAsync();
            return true;
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            _db.Entry(session).State = EntityState.Detached;
            return false;
        }
    }

    public async Task RemoveAsync(PickerSession session)
    {
        _db.PickerSessions.Remove(session);
        await _db.SaveChangesAsync();
    }

    public Task SaveChangesAsync() => _db.SaveChangesAsync();
}
