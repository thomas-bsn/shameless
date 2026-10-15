using System.Net.Http.Headers;
using Microsoft.Extensions.DependencyInjection;
using Shameless.Api.Interfaces;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>Établit une session connectée pour un joueur Discord.</summary>
public static class TestSession
{
    public const string GuildId = "424242";

    public static HttpClient CreateSignedInClient(this ApiFactory factory, string discordId = "1001", string username = "joueur")
    {
        var client = factory.CreateClient();
        var issuer = factory.Services.GetRequiredService<IJwtIssuer>();
        var token = issuer.Issue(new JwtUser(discordId, username, null, $"https://cdn.example/{discordId}.png"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);
        return client;
    }
}
