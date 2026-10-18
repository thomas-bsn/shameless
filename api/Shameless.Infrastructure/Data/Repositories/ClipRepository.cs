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

    public Task<List<Clip>> GetTousAsync(bool? sansGame, bool? masques, string? auteur, int? soireeId, int page, int pageSize) =>
        _db.Clips.AsNoTracking().Include(c => c.Game).ThenInclude(g => g!.Series)
            .Where(c => sansGame != true || c.GameId == null)
            .Where(c => masques != true || c.MasqueDuFond)
            .Where(c => string.IsNullOrWhiteSpace(auteur) || c.AuteurNom.ToLower().Contains(auteur.Trim().ToLower()))
            .Where(c => soireeId == null || (c.Game != null && c.Game.Series != null && c.Game.Series.SoireeId == soireeId))
            .OrderByDescending(c => c.PublieLe).ThenByDescending(c => c.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

    public Task<List<Clip>> GetEligiblesAuFondAsync(int take) =>
        _db.Clips.AsNoTracking().Where(c => !c.MasqueDuFond)
            .OrderBy(_ => EF.Functions.Random()).Take(take).ToListAsync();

    public Task<bool> TrySaveChangesAsync() => _db.TrySaveChangesAsync();
}
