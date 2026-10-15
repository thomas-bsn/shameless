using Shameless.Application.DTOs.Users;

namespace Shameless.Application.Interfaces;

public interface IUserRepository
{
    Task<UserDto?> GetByDiscordIdAsync(long discordId);
    Task<UserDto> CreateAsync(long discordId, string? username, string? avatarUrl);
}