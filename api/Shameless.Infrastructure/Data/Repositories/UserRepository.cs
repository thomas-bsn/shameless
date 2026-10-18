using Shameless.Application.DTOs.Users;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Shameless.Infrastructure.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public async Task<UserDto?> GetByDiscordIdAsync(long discordId)
    {
        var user = await _db.Users.AsNoTracking().OrderBy(u => u.Id).FirstOrDefaultAsync(u => u.DiscordId == discordId);
        return user == null ? null : ToDto(user);
    }

    public async Task<UserDto> CreateAsync(long discordId, string? username, string? avatarUrl)
    {
        var user = new User
        {
            DiscordId = discordId,
            DiscordUsername = username,
            AvatarUrl = avatarUrl
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        return ToDto(user);
    }

    public async Task<UserDto> SignInAsync(long discordId, string? username, string? avatarUrl)
    {
        var user = await _db.Users.OrderBy(u => u.Id).FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user is null) return await CreateAsync(discordId, username, avatarUrl);

        user.DiscordUsername = username;
        user.AvatarUrl = avatarUrl;
        await _db.SaveChangesAsync();
        return ToDto(user);
    }

    public async Task<List<UserDto>> ListAsync() =>
        (await _db.Users.AsNoTracking().OrderBy(u => u.DiscordUsername).ThenBy(u => u.Id).ToListAsync()).Select(ToDto).ToList();

    public async Task<List<UserDto>> ListByRoleAsync(UserRole role) =>
        (await _db.Users.AsNoTracking().Where(u => u.Role == role).OrderBy(u => u.Id).ToListAsync()).Select(ToDto).ToList();

    public async Task<UserDto?> SetRoleAsync(long discordId, UserRole role, string promotedBy, DateTime promotedAt, bool createIfMissing)
    {
        var user = await _db.Users.OrderBy(u => u.Id).FirstOrDefaultAsync(u => u.DiscordId == discordId);
        if (user is null)
        {
            if (!createIfMissing) return null;
            user = new User { DiscordId = discordId };
            _db.Users.Add(user);
        }

        user.Role = role;
        user.PromotedBy = role == UserRole.Utilisateur ? null : promotedBy;
        user.PromotedAt = role == UserRole.Utilisateur ? null : promotedAt;
        await _db.SaveChangesAsync();
        return ToDto(user);
    }

    public async Task<RetraitDuDernierResult> DemoteSuperadminAsync(long discordId)
    {
        // Une seule instruction : le décompte des Superadministrateurs et le retrait ne peuvent pas être séparés.
        var retires = await _db.Users
            .Where(u => u.DiscordId == discordId && u.Role == UserRole.Superadministrateur
                        && _db.Users.Count(s => s.Role == UserRole.Superadministrateur) > 1)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Role, UserRole.Utilisateur)
                .SetProperty(u => u.PromotedBy, (string?)null)
                .SetProperty(u => u.PromotedAt, (DateTime?)null));
        if (retires > 0) return RetraitDuDernierResult.Retire;

        return await _db.Users.AnyAsync(u => u.DiscordId == discordId && u.Role == UserRole.Superadministrateur)
            ? RetraitDuDernierResult.Dernier
            : RetraitDuDernierResult.PasSuperadmin;
    }

    private static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        DiscordId = u.DiscordId,
        DiscordUsername = u.DiscordUsername,
        AvatarUrl = u.AvatarUrl,
        Role = u.Role,
        PromotedBy = u.PromotedBy,
        PromotedAt = u.PromotedAt
    };
}
