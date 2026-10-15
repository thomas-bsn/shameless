// Application/DTOs/Users/UserDto.cs
namespace Shameless.Application.DTOs.Users;

public class UserDto
{
    public int Id { get; set; }
    public long DiscordId { get; set; }
    public string? DiscordUsername { get; set; }
    public string? AvatarUrl { get; set; }
}