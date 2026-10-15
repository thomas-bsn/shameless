using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

public interface ICustomGameRepository
{
    Task<CustomGame> AddAsync(CustomGame game);
    Task<CustomGame?> GetByIdAsync(int id);
    Task<CustomGame?> GetByIdWithParticipantsAsync(int id);
    Task<List<CustomGame>> GetByMonthAsync(DateTime month);
    Task<List<CustomGameParticipant>> GetPlayerGamesForMonthAsync(int playerId, DateTime month);
    Task SaveChangesAsync();
    Task<List<CustomGame>> GetPendingAsync();
    Task<List<CustomGame>> GetByYearMonthAsync(int year, int month);
    Task<List<CustomGame>> GetBySeriesIdAsync(int seriesId);
    Task DeleteBySeriesIdAsync(int seriesId);
}