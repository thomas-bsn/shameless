using Microsoft.AspNetCore.Authorization;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Api.Auth;

public static class Policies
{
    public const string Admin = "Admin";
}

/// <summary>Admin = Administrateur ou Superadministrateur en base, relu à chaque requête (ADR 0017).</summary>
public sealed class AdminRequirement : IAuthorizationRequirement;

public sealed class AdminHandler(IUserRepository users) : AuthorizationHandler<AdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        if (context.User.DiscordIdOrNull() is not { } discordId) return;

        var user = await users.GetByDiscordIdAsync(discordId);
        if (user is { Role: UserRole.Administrateur or UserRole.Superadministrateur })
            context.Succeed(requirement);
    }
}
