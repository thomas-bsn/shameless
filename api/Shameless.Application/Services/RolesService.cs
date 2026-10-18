using Shameless.Application.DTOs.Users;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

public enum RoleResult
{
    Ok,
    UserNotFound,
    /// <summary>Un Administrateur ne touche pas un Superadministrateur et ne se retire pas lui-même.</summary>
    Forbidden,
    NotSuperadmin,
    LastSuperadmin
}

/// <summary>Les trois rôles de l'ADR 0017 : promotion des Administrateurs, commande des Superadministrateurs.</summary>
public class RolesService(IUserRepository users, TimeProvider clock)
{
    public const string LigneDeCommande = "ligne de commande";
    public const string Configuration = "configuration";

    public Task<List<UserDto>> ListAsync() => users.ListAsync();

    public async Task<RoleResult> PromoteAdministratorAsync(long actorDiscordId, long targetDiscordId)
    {
        var target = await users.GetByDiscordIdAsync(targetDiscordId);
        if (target is null) return RoleResult.UserNotFound;
        if (target.Role == UserRole.Superadministrateur) return RoleResult.Forbidden;
        if (target.Role == UserRole.Administrateur) return RoleResult.Ok;

        var actor = await users.GetByDiscordIdAsync(actorDiscordId);
        await users.SetRoleAsync(targetDiscordId, UserRole.Administrateur,
            actor?.DiscordUsername ?? actorDiscordId.ToString(), Now(), createIfMissing: false);
        return RoleResult.Ok;
    }

    public async Task<RoleResult> RemoveAdministratorAsync(long actorDiscordId, long targetDiscordId)
    {
        var target = await users.GetByDiscordIdAsync(targetDiscordId);
        if (target is null) return RoleResult.UserNotFound;
        if (target.Role == UserRole.Superadministrateur || targetDiscordId == actorDiscordId) return RoleResult.Forbidden;
        if (target.Role == UserRole.Utilisateur) return RoleResult.Ok;

        await users.SetRoleAsync(targetDiscordId, UserRole.Utilisateur, "", Now(), createIfMissing: false);
        return RoleResult.Ok;
    }

    /// <summary>Au premier démarrage, les identifiants de la configuration deviennent Superadministrateurs. Sans effet dès qu'il en existe un.</summary>
    public async Task<int> ImportConfiguredSuperadminsAsync(IEnumerable<long> discordIds)
    {
        if ((await users.ListByRoleAsync(UserRole.Superadministrateur)).Count > 0) return 0;

        var count = 0;
        foreach (var id in discordIds.Distinct())
        {
            await users.SetRoleAsync(id, UserRole.Superadministrateur, Configuration, Now(), createIfMissing: true);
            count++;
        }
        return count;
    }

    /// <summary>Commande « superadmin ajouter » : idempotente, crée la ligne si la personne ne s'est pas encore connectée.</summary>
    public async Task AddSuperadminAsync(long discordId)
    {
        var existing = await users.GetByDiscordIdAsync(discordId);
        if (existing?.Role == UserRole.Superadministrateur) return;
        await users.SetRoleAsync(discordId, UserRole.Superadministrateur, LigneDeCommande, Now(), createIfMissing: true);
    }

    /// <summary>Commande « superadmin retirer » : le dernier Superadministrateur ne se retire pas.</summary>
    public async Task<RoleResult> RemoveSuperadminAsync(long discordId) =>
        await users.DemoteSuperadminAsync(discordId) switch
        {
            RetraitDuDernierResult.Retire => RoleResult.Ok,
            RetraitDuDernierResult.Dernier => RoleResult.LastSuperadmin,
            _ => RoleResult.NotSuperadmin
        };

    public Task<List<UserDto>> ListSuperadminsAsync() => users.ListByRoleAsync(UserRole.Superadministrateur);

    private DateTime Now() => clock.GetUtcNow().UtcDateTime;
}
