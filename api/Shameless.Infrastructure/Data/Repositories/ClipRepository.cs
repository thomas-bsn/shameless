using Microsoft.EntityFrameworkCore;
using Shameless.Application.Clips;
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

    public async Task<(List<Clip> Clips, int Total)> ListerAsync(FiltreDesClips filtre, int page, int taille)
    {
        var requete = _db.Clips.AsNoTrackingWithIdentityResolution().AsQueryable();
        if (!string.IsNullOrWhiteSpace(filtre.Auteur))
            requete = requete.Where(c => EF.Functions.ILike(c.AuteurNom, $"%{filtre.Auteur.Trim()}%"));
        if (filtre.SansGame is { } sansGame)
            requete = requete.Where(c => (c.GameId == null) == sansGame);
        if (filtre is { SoireeDebut: { } debut, SoireeFin: { } fin })
            requete = requete.Where(c =>
                (c.Game != null ? c.Game.CreatedAt : c.PublieLe) >= debut &&
                (c.Game != null ? c.Game.CreatedAt : c.PublieLe) < fin);

        var total = await requete.CountAsync();
        var clips = await requete
            .Include(c => c.Game).ThenInclude(g => g!.Series).ThenInclude(s => s!.Games)
            .OrderByDescending(c => c.PublieLe).ThenByDescending(c => c.Id)
            .Skip((page - 1) * taille).Take(taille)
            .ToListAsync();
        return (clips, total);
    }

    public Task<bool> TrySaveChangesAsync() => _db.TrySaveChangesAsync();
}
