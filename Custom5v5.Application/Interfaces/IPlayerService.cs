using Custom5v5.Application.DTOs.Players;

namespace Custom5v5.Application.Interfaces;

public interface IPlayerService
{
    Task<List<PlayerDto>> GetPlayersAsync();
    Task<PlayerResponse> CreatePlayerAsync(string prenom, string riotId);
    Task RefreshAllRanksAsync();
    Task LinkPlayerAsync(string discordUserId, int playerId);
    Task<PlayerDto?> GetPlayerByDiscordIdAsync(string discordUserId);
    Task LinkPlayerAsync(string discordUserId, int playerId, string? username, string? avatarUrl);
    Task UpdatePeakAsync(string discordUserId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp);
    Task RefreshAllMainRolesAsync();
    Task RefreshRiotIdsAsync();
}