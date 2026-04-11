using Custom5v5.Application.Entities;

namespace Custom5v5.Application.Interfaces;

public interface ICustomGameRepository
{
    Task<CustomGame> AddAsync(CustomGame game);
    Task<CustomGame?> GetByIdAsync(int id);
    Task<CustomGame?> GetByIdWithParticipantsAsync(int id);
    Task<List<CustomGame>> GetByMonthAsync(DateTime month);
    Task<List<CustomGameParticipant>> GetPlayerGamesForMonthAsync(int playerId, DateTime month);
    Task SaveChangesAsync();
    Task<List<CustomGame>> GetPendingAsync();
}