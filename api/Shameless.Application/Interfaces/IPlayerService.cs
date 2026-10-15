using Shameless.Application.DTOs.Players;

namespace Shameless.Application.Interfaces;

public enum LinkResult { Linked, PlayerNotFound, AlreadyLinkedToAnotherUser }

public interface IPlayerService
{
    Task<List<PlayerDto>> GetPlayersAsync();
    Task<PlayerResponse> CreatePlayerAsync(string prenom, string riotId);
    Task RefreshAllRanksAsync();
    Task<PlayerDto?> GetPlayerByDiscordIdAsync(string discordUserId);
    Task<LinkResult> LinkPlayerAsync(string discordUserId, int playerId, string? username, string? avatarUrl, bool canTakeOver);
    Task<bool> UpdatePeakAsync(string discordUserId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp);
    Task<bool> UpdatePeakForPlayerAsync(int playerId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp);
    Task<bool> SetCostAsync(int playerId, decimal cost);
    Task RefreshAllMainRolesAsync();
    Task RefreshRiotIdsAsync();
}