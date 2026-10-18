// Application/DTOs/Users/UserDto.cs
using Shameless.Application.Entities;

namespace Shameless.Application.DTOs.Users;

public class UserDto
{
    public int Id { get; set; }
    public long DiscordId { get; set; }
    public string? DiscordUsername { get; set; }
    public string? AvatarUrl { get; set; }
    public string? BannerUrl { get; set; }
    public string? BannerColor { get; set; }
    public UserRole Role { get; set; }
    public string? PromotedBy { get; set; }
    public DateTime? PromotedAt { get; set; }
}
