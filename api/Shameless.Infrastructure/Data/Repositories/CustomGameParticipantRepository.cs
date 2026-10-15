using Shameless.Application.Entities;
using Shameless.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Shameless.Infrastructure.Repositories;

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