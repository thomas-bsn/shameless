using Shameless.Application.DTOs.Players;

namespace Shameless.Application.Interfaces;

public enum PeakResult { Updated, PlayerNotFound, InvalidSeason }

public enum LinkResult { Linked, PlayerNotFound, AlreadyLinkedToAnotherPlayer }

public interface IPlayerService
{
    Task<List<PlayerDto>> GetPlayersAsync();
    Task<PlayerDto?> GetPlayerAsync(int playerId);
    Task<RiotAccountPreview?> PreviewRiotAccountAsync(string riotId);
    Task<PlayerResponse> CreatePlayerAsync(string prenom, string riotId);
    Task RefreshAllRanksAsync();
    Task<PlayerDto?> GetPlayerByDiscordIdAsync(string discordUserId);
    Task<LinkResult> LinkPlayerAsync(string discordUserId, int playerId, string? username, string? avatarUrl);
    /// <summary>Retire seulement l'association entre l'Utilisateur et son Joueur ; false s'il n'en avait pas.</summary>
    Task<bool> UnlinkPlayerAsync(string discordUserId);
    Task<PeakResult> UpdatePeakAsync(string discordUserId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp);
    Task<PeakResult> UpdatePeakForPlayerAsync(int playerId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp);
    Task RefreshAllMainRolesAsync();
    /// <summary>Relit les rangs et recalcule le rôle principal d'un joueur ; null si le joueur n'existe pas.</summary>
    Task<Shameless.Application.Riot.ResultatActualisationJoueur?> RefreshRiotAsync(int playerId);
    Task RefreshRiotIdsAsync();
}