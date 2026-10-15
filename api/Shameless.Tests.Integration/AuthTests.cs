using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class AuthTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task The_callback_sets_a_secure_session_cookie_and_redirects_without_any_token()
    {
        var client = Factory.CreateAnonymousClient();
        var code = Factory.Discord.AddUser("5001", "Zoe", TestSession.GuildId);

        var callback = await TestSession.CompleteLoginAsync(client, code, $"{TestSession.AllowedOrigin}/games");

        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal($"{TestSession.AllowedOrigin}/games", callback.Headers.Location!.ToString());

        var cookie = Assert.Single(callback.Headers.GetValues("Set-Cookie"));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);

        var me = await client.GetFromJsonAsync<JsonElement>("/auth/me", Ct);
        Assert.Equal("5001", me.GetProperty("discordUserId").GetString());
        Assert.Equal("Zoe", me.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Without_a_return_url_the_callback_redirects_to_the_site_root()
    {
        var client = Factory.CreateAnonymousClient();
        var code = Factory.Discord.AddUser("5002", "Yann", TestSession.GuildId);

        var callback = await TestSession.CompleteLoginAsync(client, code);

        Assert.Equal("/", callback.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("https://evil.example/steal")]
    [InlineData("//evil.example")]
    [InlineData("javascript:alert(1)")]
    public async Task A_return_url_outside_the_allowlist_is_refused(string returnUrl)
    {
        var client = Factory.CreateAnonymousClient();

        var login = await client.GetAsync($"/auth/discord/login?returnUrl={Uri.EscapeDataString(returnUrl)}", Ct);

        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
    }

    [Fact]
    public async Task Someone_outside_the_community_guild_cannot_sign_in()
    {
        var client = Factory.CreateAnonymousClient();
        var code = Factory.Discord.AddUser("5003", "Intrus", "other-guild");

        var callback = await TestSession.CompleteLoginAsync(client, code);

        Assert.Equal(HttpStatusCode.Forbidden, callback.StatusCode);
        Assert.False(callback.Headers.Contains("Set-Cookie"));
    }

    [Fact]
    public async Task Signing_out_ends_the_session()
    {
        var client = Factory.CreateSignedInClient("5004");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/auth/me", Ct)).StatusCode);

        var logout = await client.PostAsync("/auth/logout", null, Ct);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me", Ct)).StatusCode);
    }
}
