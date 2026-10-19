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

    /// <summary>L'identifiant Discord en nombre, ou null sans session (ou identifiant illisible).</summary>
    public static long? DiscordIdOrNull(this ClaimsPrincipal user) =>
        long.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    public static string? Username(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.Name);

    public static string? AvatarUrl(this ClaimsPrincipal user) => user.FindFirstValue(AvatarClaim);

    public static async Task<bool> IsAdminAsync(this IAuthorizationService authorization, ClaimsPrincipal user) =>
        (await authorization.AuthorizeAsync(user, Policies.Admin)).Succeeded;
}
