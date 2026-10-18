using Shameless.Application.DTOs.Users;
using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

public interface IUserRepository
{
    Task<UserDto?> GetByDiscordIdAsync(long discordId);
    Task<UserDto> CreateAsync(long discordId, string? username, string? avatarUrl);

    /// <summary>Première connexion : crée la ligne ; ensuite met à jour pseudo et photo.</summary>
    Task<UserDto> SignInAsync(long discordId, string? username, string? avatarUrl);

    Task<List<UserDto>> ListAsync();
    Task<List<UserDto>> ListByRoleAsync(UserRole role);

    /// <summary>
    /// Donne ce rôle avec la mention de promotion (effacée pour le rôle utilisateur). Crée la ligne si
    /// l'Utilisateur ne s'est jamais connecté et que <paramref name="createIfMissing"/> le permet.
    /// </summary>
    Task<UserDto?> SetRoleAsync(long discordId, UserRole role, string promotedBy, DateTime promotedAt, bool createIfMissing);

    /// <summary>Remet un Superadministrateur au rang d'utilisateur, sauf s'il est le dernier (une seule instruction SQL).</summary>
    Task<RetraitDuDernierResult> DemoteSuperadminAsync(long discordId);
}

public enum RetraitDuDernierResult { Retire, PasSuperadmin, Dernier }
