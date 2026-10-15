using Custom5v5.Application.DTOs.Users;
using Custom5v5.Application.Entities;
using Custom5v5.Application.Interfaces;
using Custom5v5.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Custom5v5.Infrastructure.Data.Repositories;

public class UserRepository : IUserRepository
{
    private readonly AppDbContext _db;

    public UserRepository(AppDbContext db) => _db = db;

    public async Task<UserDto?> GetByDiscordIdAsync(long discordId)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.DiscordId == discordId);
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

    private static UserDto ToDto(User u) => new()
    {
        Id = u.Id,
        DiscordId = u.DiscordId,
        DiscordUsername = u.DiscordUsername,
        AvatarUrl = u.AvatarUrl
    };
}