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

    [Fact]
    public async Task Signing_in_stores_the_discord_profile_with_an_animated_banner_as_a_gif()
    {
        var client = Factory.CreateAnonymousClient();
        var code = Factory.Discord.AddProfile("5101", "Lou", "av1", "a_ban1", 0x112233, TestSession.GuildId);

        await TestSession.CompleteLoginAsync(client, code);

        var me = await client.GetFromJsonAsync<JsonElement>("/auth/me", Ct);
        Assert.Equal("https://cdn.discordapp.com/avatars/5101/av1.png", me.GetProperty("avatarUrl").GetString());
        Assert.Equal("https://cdn.discordapp.com/banners/5101/a_ban1.gif", me.GetProperty("bannerUrl").GetString());
        Assert.Equal("#112233", me.GetProperty("bannerColor").GetString());
    }

    [Fact]
    public async Task A_static_banner_is_a_png_and_no_banner_means_none()
    {
        var withBanner = Factory.CreateAnonymousClient();
        await TestSession.CompleteLoginAsync(withBanner, Factory.Discord.AddProfile("5102", "Max", null, "ban2", null, TestSession.GuildId));
        var me = await withBanner.GetFromJsonAsync<JsonElement>("/auth/me", Ct);
        Assert.Equal("https://cdn.discordapp.com/banners/5102/ban2.png", me.GetProperty("bannerUrl").GetString());
        Assert.Equal(JsonValueKind.Null, me.GetProperty("bannerColor").ValueKind);

        var without = Factory.CreateSignedInClient("5103");
        var other = await without.GetFromJsonAsync<JsonElement>("/auth/me", Ct);
        Assert.Equal(JsonValueKind.Null, other.GetProperty("bannerUrl").ValueKind);
    }

    [Fact]
    public async Task Each_sign_in_refreshes_pseudo_photo_and_banner()
    {
        var client = Factory.CreateAnonymousClient();
        await TestSession.CompleteLoginAsync(client, Factory.Discord.AddProfile("5104", "Avant", "old", "oldban", null, TestSession.GuildId));

        await TestSession.CompleteLoginAsync(client, Factory.Discord.AddProfile("5104", "Apres", "new", "newban", null, TestSession.GuildId));

        var me = await client.GetFromJsonAsync<JsonElement>("/auth/me", Ct);
        Assert.Equal("Apres", me.GetProperty("username").GetString());
        Assert.EndsWith("/5104/new.png", me.GetProperty("avatarUrl").GetString());
        Assert.EndsWith("/5104/newban.png", me.GetProperty("bannerUrl").GetString());
    }

    [Fact]
    public async Task Refreshing_rereads_discord_without_a_new_login_and_is_visible_immediately()
    {
        var client = Factory.CreateAnonymousClient();
        await TestSession.CompleteLoginAsync(client, Factory.Discord.AddProfile("5105", "Avant", "old", null, null, TestSession.GuildId));
        var returnUrl = $"{TestSession.AllowedOrigin}/compte";

        var start = await client.GetAsync($"/auth/discord/refresh?returnUrl={Uri.EscapeDataString(returnUrl)}", Ct);
        Assert.Equal(HttpStatusCode.Redirect, start.StatusCode);
        Assert.Contains("prompt=none", start.Headers.Location!.ToString());

        var code = Factory.Discord.AddProfile("5105", "Apres", "new", "newban", null, TestSession.GuildId);
        var back = await TestSession.CompleteRefreshAsync(client, code, returnUrl);

        Assert.Equal(returnUrl, back.Headers.Location!.ToString());
        var me = await client.GetFromJsonAsync<JsonElement>("/auth/me", Ct);
        Assert.Equal("Apres", me.GetProperty("username").GetString());
        Assert.EndsWith("/5105/new.png", me.GetProperty("avatarUrl").GetString());
        Assert.EndsWith("/5105/newban.png", me.GetProperty("bannerUrl").GetString());
    }

    [Fact]
    public async Task A_refresh_that_discord_refuses_keeps_the_session_and_the_old_profile()
    {
        var client = Factory.CreateAnonymousClient();
        await TestSession.CompleteLoginAsync(client, Factory.Discord.AddProfile("5106", "Stable", "old", null, null, TestSession.GuildId));
        var returnUrl = $"{TestSession.AllowedOrigin}/compte";

        // Discord ne rend pas de code (autorisation silencieuse impossible).
        var start = await client.GetAsync($"/auth/discord/refresh?returnUrl={Uri.EscapeDataString(returnUrl)}", Ct);
        var state = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(start.Headers.Location!.Query)["state"].ToString();
        var denied = await client.GetAsync($"/auth/discord/callback?error=login_required&state={Uri.EscapeDataString(state)}", Ct);
        Assert.Equal(HttpStatusCode.Redirect, denied.StatusCode);
        Assert.Equal(returnUrl, denied.Headers.Location!.ToString());

        // Discord rend un code mais l'échange échoue.
        var broken = await TestSession.CompleteRefreshAsync(client, Factory.Discord.AddFailingCode(), returnUrl);
        Assert.Equal(HttpStatusCode.Redirect, broken.StatusCode);
        Assert.Equal(returnUrl, broken.Headers.Location!.ToString());

        var me = await client.GetFromJsonAsync<JsonElement>("/auth/me", Ct);
        Assert.True(me.GetProperty("valid").GetBoolean());
        Assert.Equal("Stable", me.GetProperty("username").GetString());
    }

    [Fact]
    public async Task A_refresh_never_switches_the_session_to_another_discord_account()
    {
        var client = Factory.CreateSignedInClient("5107", "Moi");

        var back = await TestSession.CompleteRefreshAsync(client, Factory.Discord.AddUser("5108", "Autre", TestSession.GuildId), $"{TestSession.AllowedOrigin}/compte");

        Assert.Equal(HttpStatusCode.Redirect, back.StatusCode);
        var me = await client.GetFromJsonAsync<JsonElement>("/auth/me", Ct);
        Assert.Equal("5107", me.GetProperty("discordUserId").GetString());
        Assert.Equal("Moi", me.GetProperty("username").GetString());
    }

    [Fact]
    public async Task Refreshing_requires_a_session()
    {
        var response = await Factory.CreateAnonymousClient().GetAsync("/auth/discord/refresh", Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
