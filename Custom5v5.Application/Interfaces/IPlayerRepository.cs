namespace Custom5v5.Application.Interfaces;

using Custom5v5.Application.DTOs.Players;

public interface IPlayerRepository
{
    Task<List<PlayerDto>> GetAllAsync();
    Task<bool> ExistsByPuuidAsync(string puuid);
    Task<PlayerDto> AddAsync(PlayerDto player);
    Task UpdateRankAsync(int id, string? tier, int? division, int? lp);
    Task LinkUserAsync(int playerId, int userId);
    Task<PlayerDto?> GetByUserIdAsync(int userId);
    Task UpdatePeakAsync(int playerId, string peakTier, int? peakDivision, string peakSeason, int peakLp);
    Task UpdateMainRoleAsync(int id, string? mainRole);
}