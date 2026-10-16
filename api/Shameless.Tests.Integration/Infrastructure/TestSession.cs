using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>Établit une session en passant par le vrai flux OAuth, Discord étant simulé.</summary>
public static class TestSession
{
    public const string GuildId = "424242";
    public const string AdminDiscordId = "9000";
    public const string AllowedOrigin = "https://shameless.example";

    public static HttpClient CreateAnonymousClient(this ApiFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    public static HttpClient CreateSignedInClient(this ApiFactory factory, string discordId = "1001", string username = "joueur")
    {
        var client = factory.CreateAnonymousClient();
        var code = factory.Discord.AddUser(discordId, username, GuildId);
        var callback = CompleteLoginAsync(client, code).GetAwaiter().GetResult();
        if (callback.StatusCode != HttpStatusCode.Redirect)
            throw new InvalidOperationException($"Login failed: {(int)callback.StatusCode}");
        return client;
    }

    /// <summary>La clé que le bot Discord présente à l'API (identité machine bot → API, ADR 0013).</summary>
    public const string BotKey = "cle-du-bot-vers-l-api";

    /// <summary>Le bot Discord : sans session, avec sa clé en <c>Authorization: Bearer</c>.</summary>
    public static HttpClient CreateBotClient(this ApiFactory factory)
    {
        var client = factory.CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", BotKey);
        return client;
    }

    public static HttpClient CreateAdminClient(this ApiFactory factory) =>
        factory.CreateSignedInClient(AdminDiscordId, "admin");

    /// <summary>Démarre le login puis revient sur le callback avec le code donné.</summary>
    public static async Task<HttpResponseMessage> CompleteLoginAsync(HttpClient client, string code, string? returnUrl = null)
    {
        var loginUrl = returnUrl is null ? "/auth/discord/login" : $"/auth/discord/login?returnUrl={Uri.EscapeDataString(returnUrl)}";
        var login = await client.GetAsync(loginUrl);
        if (login.StatusCode != HttpStatusCode.Redirect)
            return login;

        var state = QueryHelpers.ParseQuery(login.Headers.Location!.Query)["state"].ToString();
        return await client.GetAsync($"/auth/discord/callback?code={Uri.EscapeDataString(code)}&state={Uri.EscapeDataString(state)}");
    }
}
