using Shameless.Application.DTOs.Players;

namespace Shameless.Application.Interfaces;

public enum PeakResult { Updated, PlayerNotFound, InvalidSeason }

public enum LinkResult { Linked, PlayerNotFound, AlreadyLinkedToAnotherUser }

public interface IPlayerService
{
    Task<List<PlayerDto>> GetPlayersAsync();
    Task<PlayerDto?> GetPlayerAsync(int playerId);
    Task<RiotAccountPreview?> PreviewRiotAccountAsync(string riotId);
    Task<PlayerResponse> CreatePlayerAsync(string prenom, string riotId);
    Task RefreshAllRanksAsync();
    Task<PlayerDto?> GetPlayerByDiscordIdAsync(string discordUserId);
    Task<LinkResult> LinkPlayerAsync(string discordUserId, int playerId, string? username, string? avatarUrl, bool canTakeOver);
    Task<PeakResult> UpdatePeakAsync(string discordUserId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp);
    Task<PeakResult> UpdatePeakForPlayerAsync(int playerId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp);
    Task RefreshAllMainRolesAsync();
    Task RefreshRiotIdsAsync();
}