using Shameless.Application.Entities;

namespace Shameless.Infrastructure.Repositories;

public interface ICustomGameParticipantRepository
{
    Task AddRangeAsync(List<CustomGameParticipant> participants);
    Task<List<CustomGameParticipant>> GetByGameIdAsync(int gameId);
    Task DeleteByGameIdAsync(int gameId);
 
}