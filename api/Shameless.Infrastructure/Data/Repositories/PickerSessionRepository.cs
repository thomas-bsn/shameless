using Microsoft.EntityFrameworkCore;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Data.Repositories;

public class PickerSessionRepository : IPickerSessionRepository
{
    private readonly AppDbContext _db;

    public PickerSessionRepository(AppDbContext db) => _db = db;

    public Task<PickerSession?> GetBySoireeAsync(int soireeId) =>
        _db.PickerSessions
            .Include(s => s.Players.OrderBy(p => p.Position)).ThenInclude(p => p.Player).ThenInclude(p => p.User)
            .SingleOrDefaultAsync(s => s.SoireeId == soireeId);

    public Task<bool> TryAddAsync(PickerSession session) => _db.TryAddAsync(session);

    public void Remove(PickerSession session) => _db.PickerSessions.Remove(session);

    public Task<bool> TrySaveChangesAsync() => _db.TrySaveChangesAsync();
}
