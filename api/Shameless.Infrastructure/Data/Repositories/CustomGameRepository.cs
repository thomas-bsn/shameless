using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Shameless.Infrastructure.Repositories;

public class CustomGameRepository : ICustomGameRepository
{
    private readonly AppDbContext _db;

    public CustomGameRepository(AppDbContext db)
    {
        _db = db;
    }

    public void Add(CustomGame game) => _db.CustomGames.Add(game);

    public Task<RapportCompanion?> GetRapportAsync(int id) =>
        _db.RapportsCompanion.Include(r => r.Game).SingleOrDefaultAsync(r => r.Id == id);

    public Task<RapportCompanion?> GetRapportByRiotMatchIdAsync(string riotMatchId) =>
        _db.RapportsCompanion
            .AsNoTracking()
            .Include(r => r.Game).ThenInclude(g => g.Series)
            .SingleOrDefaultAsync(r => r.RiotMatchId == riotMatchId);

    public async Task<CustomGame?> GetByIdAsync(int id)
    {
        return await _db.CustomGames.FindAsync(id);
    }

    public async Task<CustomGame?> GetByIdWithParticipantsAsync(int id)
    {
        return await _db.CustomGames
            .Include(g => g.Participants)
                .ThenInclude(p => p.Player)
            .FirstOrDefaultAsync(g => g.Id == id);
    }

    public Task<CustomGame?> GetByRiotMatchIdAsync(string riotMatchId) =>
        _db.CustomGames
            .AsNoTracking()
            .Include(g => g.Series)
            .SingleOrDefaultAsync(g => g.RiotMatchId == riotMatchId);

    public async Task<List<CustomGame>> GetByMonthAsync(DateTime month)
    {
        var startOfMonth = new DateTime(month.Year, month.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var startOfNextMonth = startOfMonth.AddMonths(1);
    
        return await _db.CustomGames
            .Include(g => g.Participants)
            .ThenInclude(p => p.Player)
            .Where(g => g.PlayedAt >= startOfMonth && g.PlayedAt < startOfNextMonth)
            .OrderByDescending(g => g.PlayedAt)
            .ToListAsync();
    }

    public async Task<List<CustomGame>> GetCompletedGamesOfPlayerForMonthAsync(int playerId, DateTime month)
    {
        return await _db.CustomGames
            .Include(g => g.Participants)
            .ThenInclude(p => p.Player)
            .Include(g => g.Series)
            .ThenInclude(s => s!.Players) // rôle de l'Autorole, repli de la note de performance
            .Where(g => g.Status == GameStatuses.Completed
                        && g.PlayedAt.Year == month.Year
                        && g.PlayedAt.Month == month.Month
                        && g.Participants.Any(p => p.PlayerId == playerId))
            .OrderBy(g => g.PlayedAt)
            .ToListAsync();
    }

    public async Task<List<CustomGame>> GetPendingAsync()
    {
        return await _db.CustomGames
            .Include(g => g.Participants)
            .ThenInclude(p => p.Player)
            .Where(g => g.Status == GameStatuses.Pending)
            .OrderByDescending(g => g.PlayedAt)
            .ToListAsync();
    }
    
    public async Task<List<CustomGame>> GetBySeriesIdAsync(int seriesId)
    {
        return await _db.CustomGames
            .Include(g => g.Participants)
            .ThenInclude(p => p.Player)
            .Where(g => g.SeriesId == seriesId)
            .OrderBy(g => g.Id)
            .ToListAsync();
    }
    
    public async Task<List<CustomGameParticipant>> GetCompletedParticipationsAsync() =>
        await _db.CustomGameParticipants
            .Include(p => p.CustomGame)
            .Where(p => p.CustomGame.Status == GameStatuses.Completed)
            .ToListAsync();

    public async Task<List<CustomGame>> GetByYearMonthAsync(int year, int month)
    {
        return await _db.CustomGames
            .Include(g => g.Participants)
            .ThenInclude(p => p.Player)
            .Include(g => g.Series)
            .ThenInclude(s => s!.Players) // rôle de l'Autorole, repli de la note de performance
            .Where(g => g.PlayedAt.Year == year
                        && g.PlayedAt.Month == month
                        && g.Status == GameStatuses.Completed)
            .OrderByDescending(g => g.PlayedAt)
            .ToListAsync();
    }
    
    public void Remove(CustomGame game) => _db.CustomGames.Remove(game);

    public Task<bool> TrySaveChangesAsync() => _db.TrySaveChangesAsync();
}