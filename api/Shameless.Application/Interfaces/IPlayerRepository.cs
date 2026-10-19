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
    /// <summary>Lie l'Utilisateur au Joueur (sans effet s'il l'est déjà) et recalcule le Discord principal.</summary>
    Task LinkUserAsync(int playerId, int userId);
    /// <summary>Retire l'association Utilisateur-Joueur ; true si un Joueur était lié à cet Utilisateur.</summary>
    Task<bool> UnlinkUserAsync(int userId);
    /// <summary>Le Joueur de cet Utilisateur, avec le Discord principal pour la photo ; null sans lien.</summary>
    Task<PlayerDto?> GetByUserIdAsync(int userId);

    /// <summary>Le Joueur lié à ce compte Discord (n'importe lequel de ses comptes), ou null.</summary>
    Task<int?> GetPlayerIdByDiscordIdAsync(long discordId);
    Task UpdatePeakAsync(int playerId, string? peakTier, int? peakDivision, string? peakSeason, int peakLp);
    Task UpdateMainRoleAsync(int id, string? mainRole);
    Task<Player?> GetByIdAsync(int id);

    /// <summary>Le joueur avec son Discord principal (User) et tous ses Comptes Discord liés (Users, du plus ancien au plus récent), ou null.</summary>
    Task<Player?> GetWithUserAsync(int id);
    Task<List<Player>> GetByIdsAsync(List<int> ids);

    /// <summary>Les joueurs liés à l'un de ces comptes Discord, avec leur Discord principal (User) et tous leurs comptes (Users).</summary>
    Task<List<Player>> GetByDiscordIdsAsync(IReadOnlyCollection<long> discordIds);

    /// <summary>Les joueurs enregistrés parmi ces PUUID.</summary>
    Task<List<Player>> GetByPuuidsAsync(IReadOnlyCollection<string> puuids);
    Task<List<Player>> GetAllEntitiesAsync(); 
    Task UpdateAsync(Player player);
    Task UpdateRangeAsync(List<Player> players);
    Task SaveChangesAsync();
    Task UpdateRiotIdAsync(int id, string riotId);
    /// <summary>Remplace le PUUID d'un joueur (résolu à nouveau depuis son RiotId).</summary>
    Task UpdatePuuidAsync(int id, string puuid);
}