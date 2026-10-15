using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Shameless.Api.Interfaces;
using Shameless.Application.DTOs.Discord;
using Microsoft.Extensions.Options;
using Shameless.Infrastructure.Options;

namespace Shameless.Infrastructure.Integrations.Discord;

public sealed class DiscordOAuthClient : IDiscordOAuthClient
{
    private readonly HttpClient _http;
    private readonly DiscordOAuthOptions _opt;

    public DiscordOAuthClient(HttpClient http, IOptions<DiscordOAuthOptions> options)
    {
        _http = http;
        _opt = options.Value;
    }

    public async Task<DiscordTokenResponse> ExchangeCodeAsync(string code, CancellationToken ct)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _opt.ClientId,
            ["client_secret"] = _opt.ClientSecret,
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = _opt.RedirectUri
        });

        using var response = await _http.PostAsync("oauth2/token", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Discord token exchange failed ({(int)response.StatusCode}): {body}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<DiscordTokenResponse>(json)
               ?? throw new InvalidOperationException("Invalid token response from Discord.");
    }

    public async Task<DiscordMeResponse> GetCurrentUserAsync(string accessToken, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "users/@me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _http.SendAsync(request, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Discord /users/@me failed ({(int)response.StatusCode}): {body}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<DiscordMeResponse>(json)
               ?? throw new InvalidOperationException("Invalid /users/@me response from Discord.");
    }

    public async Task<bool> IsMemberOfGuildAsync(string accessToken, string guildId, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, "users/@me/guilds");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await _http.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return false;

        var json = await res.Content.ReadAsStringAsync(ct);
        var guilds = JsonSerializer.Deserialize<List<DiscordGuild>>(json) ?? new();
        return guilds.Any(g => g.Id == guildId);
    }

    public async Task<DiscordTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
            throw new InvalidOperationException("Missing refresh token.");

        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = _opt.ClientId,
            ["client_secret"] = _opt.ClientSecret,
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken
        });

        using var response = await _http.PostAsync("oauth2/token", content, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new InvalidOperationException($"Discord token refresh failed ({(int)response.StatusCode}): {body}");
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        return JsonSerializer.Deserialize<DiscordTokenResponse>(json)
               ?? throw new InvalidOperationException("Invalid refresh token response from Discord.");
    }
}