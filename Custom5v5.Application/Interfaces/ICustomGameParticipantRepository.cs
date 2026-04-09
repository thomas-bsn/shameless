using Custom5v5.Application.Entities;

namespace Custom5v5.Infrastructure.Repositories;

public interface ICustomGameParticipantRepository
{
    Task AddRangeAsync(List<CustomGameParticipant> participants);
    Task<List<CustomGameParticipant>> GetByGameIdAsync(int gameId);
}