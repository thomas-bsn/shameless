using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class AuthorizationTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static readonly string[] AnonymousEndpoints =
    [
        "GET auth/discord/login",
        "GET auth/discord/callback",
        "GET health"
    ];

    public static TheoryData<string> AdminActions() =>
    [
        "PUT games/{0}/result",
        "PUT series/{1}/abandon",
        "PUT players/{2}/cost",
        "POST players/{2}/peak",
        "DELETE players/{2}/peak"
    ];

    [Fact]
    public async Task Every_endpoint_outside_login_callback_and_health_requires_a_session()
    {
        var client = Factory.CreateAnonymousClient();
        var endpoints = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => (Method: method, Route: e.RoutePattern.RawText!.TrimStart('/'))))
            .Where(e => !AnonymousEndpoints.Contains($"{e.Method} {e.Route}"))
            .ToList();

        Assert.NotEmpty(endpoints);
        foreach (var (method, route) in endpoints)
        {
            var url = "/" + System.Text.RegularExpressions.Regex.Replace(route, @"\{[^}]+\}", "1");
            var request = new HttpRequestMessage(new HttpMethod(method), url)
            {
                Content = method is "GET" or "DELETE" ? null : JsonContent.Create(new { })
            };

            var response = await client.SendAsync(request, Ct);

            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
                $"{method} {url} returned {(int)response.StatusCode} without a session");
        }
    }

    [Theory]
    [MemberData(nameof(AdminActions))]
    public async Task A_non_admin_player_is_refused_admin_actions(string action)
    {
        var (method, url) = await PrepareAdminActionAsync(action);
        var client = Factory.CreateSignedInClient("7001");

        var response = await client.SendAsync(AdminRequest(method, url), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminActions))]
    public async Task An_admin_can_perform_admin_actions(string action)
    {
        var (method, url) = await PrepareAdminActionAsync(action);
        var client = Factory.CreateAdminClient();

        var response = await client.SendAsync(AdminRequest(method, url), Ct);

        Assert.True(response.IsSuccessStatusCode, $"{method} {url} returned {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Me_tells_whether_the_signed_in_user_is_an_admin()
    {
        var player = await Factory.CreateSignedInClient("7002").GetFromJsonAsync<JsonElement>("/auth/me", Ct);
        var admin = await Factory.CreateAdminClient().GetFromJsonAsync<JsonElement>("/auth/me", Ct);

        Assert.False(player.GetProperty("isAdmin").GetBoolean());
        Assert.True(admin.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task An_admin_sets_a_players_cost_and_peak()
    {
        var admin = Factory.CreateAdminClient();
        var playerId = await CreatePlayerAsync(admin, "Cible");

        await ReadJsonAsync(await admin.PutAsJsonAsync($"/players/{playerId}/cost", new { cost = 5 }, Ct));
        await ReadJsonAsync(await admin.PostAsJsonAsync($"/players/{playerId}/peak",
            new { peakTier = "MASTER", peakDivision = (int?)null, peakSeason = "S2025", peakLp = 120 }, Ct));

        var players = await admin.GetFromJsonAsync<JsonElement>("/players", Ct);
        var player = Assert.Single(players.EnumerateArray());
        Assert.Equal(5m, player.GetProperty("currentCost").GetDecimal());
        Assert.Equal("MASTER", player.GetProperty("peakTier").GetString());
        Assert.Equal(120, player.GetProperty("peakLp").GetInt32());

        await ReadJsonAsync(await admin.DeleteAsync($"/players/{playerId}/peak", Ct));
        player = Assert.Single((await admin.GetFromJsonAsync<JsonElement>("/players", Ct)).EnumerateArray());
        Assert.Equal(JsonValueKind.Null, player.GetProperty("peakTier").ValueKind);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    public async Task A_cost_outside_1_to_6_is_rejected(int cost)
    {
        var admin = Factory.CreateAdminClient();
        var playerId = await CreatePlayerAsync(admin, "Cible");

        var response = await admin.PutAsJsonAsync($"/players/{playerId}/cost", new { cost }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Linking_a_player_already_linked_to_someone_else_is_refused_unless_admin()
    {
        var owner = Factory.CreateSignedInClient("7003");
        var playerId = await CreatePlayerAsync(owner, "Pris");
        await ReadJsonAsync(await owner.PostAsJsonAsync("/players/link", new { playerId }, Ct));

        var other = Factory.CreateSignedInClient("7004");
        var stolen = await other.PostAsJsonAsync("/players/link", new { playerId }, Ct);
        Assert.Equal(HttpStatusCode.Conflict, stolen.StatusCode);

        // Le propriétaire peut re-lier son propre joueur, l'admin peut ré-attribuer.
        Assert.True((await owner.PostAsJsonAsync("/players/link", new { playerId }, Ct)).IsSuccessStatusCode);
        Assert.True((await Factory.CreateAdminClient().PostAsJsonAsync("/players/link", new { playerId }, Ct)).IsSuccessStatusCode);
    }

    private async Task<(string Method, string Url)> PrepareAdminActionAsync(string action)
    {
        var admin = Factory.CreateAdminClient();
        var ids = await CreatePlayersAsync(admin, "P1", "P2");
        var game = await ReadJsonAsync(await admin.PostAsJsonAsync("/games",
            new { blueTeam = new[] { ids[0] }, redTeam = new[] { ids[1] }, boFormat = 3 }, Ct));

        var parts = action.Split(' ');
        var url = "/" + string.Format(parts[1],
            game.GetProperty("gameId").GetInt32(),
            game.GetProperty("seriesId").GetInt32(),
            ids[0]);
        return (parts[0], url);
    }

    private static HttpRequestMessage AdminRequest(string method, string url) =>
        new(new HttpMethod(method), url)
        {
            Content = method switch
            {
                "DELETE" => null,
                _ when url.EndsWith("/result") => JsonContent.Create(new { winner = "blue" }),
                _ when url.EndsWith("/cost") => JsonContent.Create(new { cost = 3 }),
                _ when url.EndsWith("/peak") => JsonContent.Create(new { peakTier = "GOLD", peakDivision = 1, peakSeason = "S2025", peakLp = 10 }),
                _ => null
            }
        };
}
