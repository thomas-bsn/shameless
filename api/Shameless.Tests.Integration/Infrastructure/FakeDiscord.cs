using Shameless.Api.Interfaces;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>Faux Discord : chaque code OAuth correspond à un utilisateur et à ses guildes.</summary>
public sealed class FakeDiscord : IDiscordOAuthClient
{
    private readonly Dictionary<string, (DiscordMeResponse User, string[] Guilds)> _byCode = new();

    public string AddUser(string discordId, string username, params string[] guilds)
    {
        var code = $"code-{discordId}-{Guid.NewGuid():N}";
        _byCode[code] = (new DiscordMeResponse(discordId, username, null, null), guilds);
        return code;
    }

    public void Reset() => _byCode.Clear();

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
