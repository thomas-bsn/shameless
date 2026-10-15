using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;

namespace Shameless.Api.Auth;

/// <summary>Lecture des claims posés à la connexion Discord.</summary>
public static class ClaimsPrincipalExtensions
{
    public const string AvatarClaim = "avatar_url";

    /// <summary>Toujours présent : la politique par défaut exige une session.</summary>
    public static string DiscordId(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new InvalidOperationException("Signed-in user without a Discord id.");

    public static string? Username(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Name);

    public static string? AvatarUrl(this ClaimsPrincipal user) => user.FindFirstValue(AvatarClaim);

    public static async Task<bool> IsAdminAsync(this IAuthorizationService authorization, ClaimsPrincipal user) =>
        (await authorization.AuthorizeAsync(user, Policies.Admin)).Succeeded;
}
