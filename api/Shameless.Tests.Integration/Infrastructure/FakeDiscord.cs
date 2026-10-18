using Shameless.Api.Interfaces;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>Faux Discord : chaque code OAuth correspond à un utilisateur et à ses guildes.</summary>
public sealed class FakeDiscord : IDiscordOAuthClient
{
    private readonly Dictionary<string, (DiscordMeResponse User, string[] Guilds)> _byCode = new();
    private readonly HashSet<string> _failing = [];

    public string AddUser(string discordId, string username, params string[] guilds) =>
        AddProfile(discordId, username, null, null, null, guilds);

    /// <summary>Un code OAuth pour ce profil Discord complet (photo, bannière, couleur d'accent).</summary>
    public string AddProfile(string discordId, string username, string? avatar, string? banner, int? accentColor, params string[] guilds)
    {
        var code = $"code-{discordId}-{Guid.NewGuid():N}";
        _byCode[code] = (new DiscordMeResponse(discordId, username, null, avatar, banner, accentColor), guilds);
        return code;
    }

    /// <summary>Un code que Discord refuse d'échanger (Discord en panne ou autorisation retirée).</summary>
    public string AddFailingCode()
    {
        var code = $"code-failing-{Guid.NewGuid():N}";
        _failing.Add(code);
        return code;
    }

    public void Reset()
    {
        _byCode.Clear();
        _failing.Clear();
    }

    public Task<DiscordTokenResponse> ExchangeCodeAsync(string code, CancellationToken ct) =>
        _byCode.ContainsKey(code)
            ? Task.FromResult(new DiscordTokenResponse(code, "Bearer", 3600, null, "identify guilds"))
            : throw new InvalidOperationException("Unknown OAuth code");

    public Task<DiscordMeResponse> GetCurrentUserAsync(string accessToken, CancellationToken ct) =>
        Task.FromResult(_byCode[accessToken].User);

    public Task<bool> IsMemberOfGuildAsync(string accessToken, string guildId, CancellationToken ct) =>
        Task.FromResult(_byCode[accessToken].Guilds.Contains(guildId));

    public Task<DiscordTokenResponse> RefreshTokenAsync(string refreshToken, CancellationToken ct) =>
        throw new NotSupportedException();
}
