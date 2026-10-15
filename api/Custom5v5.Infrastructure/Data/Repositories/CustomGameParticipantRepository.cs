using Custom5v5.Application.Entities;
using Custom5v5.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Custom5v5.Infrastructure.Repositories;

public class CustomGameParticipantRepository : ICustomGameParticipantRepository
{
    private readonly AppDbContext _db;

    public CustomGameParticipantRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task AddRangeAsync(List<CustomGameParticipant> participants)
    {
        _db.CustomGameParticipants.AddRange(participants);
        await _db.SaveChangesAsync();
    }

    public async Task<List<CustomGameParticipant>> GetByGameIdAsync(int gameId)
    {
        return await _db.CustomGameParticipants
            .Include(p => p.Player)
            .Where(p => p.CustomGameId == gameId)
            .ToListAsync();
    }
    
    public async Task DeleteByGameIdAsync(int gameId)
    {
        var participants = await _db.CustomGameParticipants.Where(p => p.CustomGameId == gameId).ToListAsync();
        _db.CustomGameParticipants.RemoveRange(participants);
        await _db.SaveChangesAsync();
    }
}