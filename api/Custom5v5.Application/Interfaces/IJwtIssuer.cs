namespace Custom5v5.Api.Interfaces;

public interface IJwtIssuer
{
    JwtIssueResult Issue(JwtUser user);
}

public sealed record JwtUser(
    string DiscordUserId,
    string Username,
    string? Discriminator,
    string? AvatarUrl
);

public sealed record JwtIssueResult(
    string AccessToken,
    DateTime ExpiresAtUtc
);