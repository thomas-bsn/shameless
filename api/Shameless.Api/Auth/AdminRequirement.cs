using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Shameless.Infrastructure.Options;

namespace Shameless.Api.Auth;

public static class Policies
{
    public const string Admin = "Admin";
}

/// <summary>Admin = ID Discord présent dans Auth:AdminDiscordIds, relu à chaque requête.</summary>
public sealed class AdminRequirement : IAuthorizationRequirement;

public sealed class AdminHandler(IOptionsMonitor<AuthOptions> options) : AuthorizationHandler<AdminRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, AdminRequirement requirement)
    {
        var discordId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (discordId is not null && options.CurrentValue.AdminIds.Contains(discordId))
            context.Succeed(requirement);

        return Task.CompletedTask;
    }
}
