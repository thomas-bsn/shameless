using Shameless.Application.DTOs.Users;
using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.Auth;

/// <summary>Un Utilisateur et son rôle ; <c>PromotedBy</c> et <c>PromotedAt</c> disent d'où vient le droit (« promu par X, le JJ/MM/AAAA »).</summary>
public sealed record UtilisateurResponse(
    string DiscordUserId,
    string? Username,
    string? AvatarUrl,
    UserRole Role,
    string? PromotedBy,
    DateTime? PromotedAt)
{
    public static UtilisateurResponse From(UserDto u) =>
        new(u.DiscordId.ToString(), u.DiscordUsername, u.AvatarUrl, u.Role, u.PromotedBy, u.PromotedAt);
}
