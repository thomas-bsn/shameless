using Microsoft.EntityFrameworkCore;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Data.Repositories;

public sealed class ClipRepository : IClipRepository
{
    private readonly AppDbContext _db;

    public ClipRepository(AppDbContext db) => _db = db;

    public void Add(Clip clip) => _db.Clips.Add(clip);

    public Task<Clip?> GetAsync(int id) => _db.Clips.SingleOrDefaultAsync(c => c.Id == id);

    public Task<Clip?> GetByAttachmentIdAsync(long discordAttachmentId) =>
        _db.Clips.AsNoTracking().SingleOrDefaultAsync(c => c.DiscordAttachmentId == discordAttachmentId);

    public Task<List<Clip>> GetDeLaGameAsync(int gameId) =>
        _db.Clips.AsNoTracking().Where(c => c.GameId == gameId).OrderBy(c => c.PublieLe).ThenBy(c => c.Id).ToListAsync();

    public Task<List<Clip>> GetSansGameAsync() =>
        _db.Clips.AsNoTracking().Where(c => c.GameId == null).OrderBy(c => c.PublieLe).ThenBy(c => c.Id).ToListAsync();

    public Task<List<Clip>> GetDuFondAuHasardAsync() =>
        _db.Clips.AsNoTracking().Where(c => !c.MasqueDuFond).OrderBy(_ => EF.Functions.Random()).ToListAsync();

    public Task<bool> TrySaveChangesAsync() => _db.TrySaveChangesAsync();
}
