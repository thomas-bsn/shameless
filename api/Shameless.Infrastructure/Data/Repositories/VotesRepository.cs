using Microsoft.EntityFrameworkCore;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Infrastructure.Data.Repositories;

public sealed class VotesRepository : IVotesRepository
{
    private readonly AppDbContext _db;

    public VotesRepository(AppDbContext db) => _db = db;

    public Task<List<BulletinDeVote>> GetBulletinsAsync(int gameId) =>
        _db.BulletinsDeVote.AsNoTracking().Where(b => b.GameId == gameId).ToListAsync();

    public Task<List<BulletinDeVote>> GetBulletinsDesGamesAsync(IReadOnlyCollection<int> gameIds) =>
        _db.BulletinsDeVote.AsNoTracking().Where(b => gameIds.Contains(b.GameId)).ToListAsync();

    public Task<BulletinDeVote?> GetBulletinAsync(int gameId, long votantDiscordId) =>
        _db.BulletinsDeVote.SingleOrDefaultAsync(b => b.GameId == gameId && b.VotantDiscordId == votantDiscordId);

    public void Add(BulletinDeVote bulletin) => _db.BulletinsDeVote.Add(bulletin);

    public void Remove(BulletinDeVote bulletin) => _db.BulletinsDeVote.Remove(bulletin);

    public Task<List<SpectateurDeGame>> GetSpectateursAsync(int gameId) =>
        _db.SpectateursDeGame.Where(s => s.GameId == gameId).ToListAsync();

    public Task<bool> EstSpectateurAsync(int gameId, long discordId) =>
        _db.SpectateursDeGame.AnyAsync(s => s.GameId == gameId && s.DiscordId == discordId);

    public void AddSpectateur(SpectateurDeGame spectateur) => _db.SpectateursDeGame.Add(spectateur);

    public void RemoveSpectateur(SpectateurDeGame spectateur) => _db.SpectateursDeGame.Remove(spectateur);

    public Task<bool> TrySaveChangesAsync() => _db.TrySaveChangesAsync();
}
