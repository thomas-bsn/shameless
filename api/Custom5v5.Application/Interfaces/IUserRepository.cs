using Custom5v5.Application.DTOs.Users;

namespace Custom5v5.Application.Interfaces;

public interface IUserRepository
{
    Task<UserDto?> GetByDiscordIdAsync(long discordId);
    Task<UserDto> CreateAsync(long discordId, string? username, string? avatarUrl);
}