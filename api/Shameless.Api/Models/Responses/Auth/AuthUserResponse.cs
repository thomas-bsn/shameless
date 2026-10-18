using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.Auth;

public sealed record AuthUserResponse(
    bool Valid,
    string? DiscordUserId,
    string? Username,
    string? AvatarUrl,
    bool IsAdmin,
    UserRole Role,
    string? BannerUrl,
    string? BannerColor
);
