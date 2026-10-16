using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

using Shameless.Application.DTOs.Players;

public interface IPlayerRepository
{
    Task<List<PlayerDto>> GetAllAsync();
    Task<PlayerDto?> GetDtoByIdAsync(int id);
    Task<bool> ExistsByPuuidAsync(string puuid);
    Task<PlayerDto> AddAsync(PlayerDto player);
    Task UpdateRankAsync(int id, string? tier, int? division, int? lp);
    Task UpdateFlexRankAsync(int id, string? tier, int? division, int? lp);
    Task LinkUserAsync(int playerId, int userId);
    Task<PlayerDto?> GetByUserIdAsync(int userId);
    Task UpdatePeakAsync(int playerId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp);
    Task UpdateMainRoleAsync(int id, string? mainRole);
    Task<Player?> GetByIdAsync(int id);

    /// <summary>Le joueur avec son compte Discord lié (User), ou null.</summary>
    Task<Player?> GetWithUserAsync(int id);
    Task<List<Player>> GetByIdsAsync(List<int> ids);

    /// <summary>Les joueurs enregistrés parmi ces PUUID.</summary>
    Task<List<Player>> GetByPuuidsAsync(IReadOnlyCollection<string> puuids);
    Task<List<Player>> GetAllEntitiesAsync(); 
    Task UpdateAsync(Player player);
    Task UpdateRangeAsync(List<Player> players);
    Task SaveChangesAsync();
    Task UpdateRiotIdAsync(int id, string riotId);
}