using System.Text.Json.Serialization;

namespace Custom5v5.Api.Interfaces;

public interface IDiscordOAuthClient
{
    Task<DiscordTokenResponse> ExchangeCodeAsync(string code, CancellationToken ct);
    Task<DiscordMeResponse> GetCurrentUserAsync(string accessToken, CancellationToken ct);
    
    Task<DiscordTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct);
    Task<bool> IsMemberOfGuildAsync(string accessToken, string guildId, CancellationToken ct);
}

public sealed record DiscordTokenResponse(
    [property: JsonPropertyName("access_token")] string AccessToken,
    [property: JsonPropertyName("token_type")] string TokenType,
    [property: JsonPropertyName("expires_in")] int ExpiresIn,
    [property: JsonPropertyName("refresh_token")] string? RefreshToken,
    [property: JsonPropertyName("scope")] string Scope
);

public sealed record DiscordMeResponse(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("username")] string Username,
    [property: JsonPropertyName("discriminator")] string? Discriminator,
    [property: JsonPropertyName("avatar")] string? Avatar
);