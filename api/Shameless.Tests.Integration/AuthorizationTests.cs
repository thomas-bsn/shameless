using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Shameless.Api.Auth;
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
        "PUT players/{2}/cost",
        "POST players/{2}/peak",
        "DELETE players/{2}/peak"
    ];

    /// <summary>Les routes de l'API ; <paramref name="parLien"/> : celles ouvertes à un lien d'accès (ADR 0010), ou les autres.</summary>
    private List<(string Method, string Route)> Endpoints(bool parLien) =>
        Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => (e.Metadata.GetMetadata<AccesParLienAttribute>() != null) == parLien)
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => (Method: method, Route: e.RoutePattern.RawText!.TrimStart('/'))))
            .Where(e => !AnonymousEndpoints.Contains($"{e.Method} {e.Route}"))
            .ToList();

    private static HttpRequestMessage AnyRequest(string method, string route) =>
        new(new HttpMethod(method), "/" + System.Text.RegularExpressions.Regex.Replace(route, @"\{[^}]+\}", "1"))
        {
            Content = method is "GET" or "DELETE" ? null : JsonContent.Create(new { })
        };

    [Fact]
    public async Task Every_endpoint_outside_login_callback_health_and_access_links_requires_a_session()
    {
        var client = Factory.CreateAnonymousClient();
        var endpoints = Endpoints(parLien: false);

        Assert.NotEmpty(endpoints);
        foreach (var (method, route) in endpoints)
        {
            var response = await client.SendAsync(AnyRequest(method, route), Ct);

            Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
                $"{method} {route} returned {(int)response.StatusCode} without a session");
        }
    }

    [Fact]
    public async Task Sans_session_une_route_ouverte_aux_liens_refuse_un_lien_inconnu()
    {
        var client = Factory.CreateAnonymousClient();
        var endpoints = Endpoints(parLien: true);

        Assert.NotEmpty(endpoints);
        foreach (var (method, route) in endpoints)
        {
            Assert.Contains($"{{{AccesParLienAttribute.RouteParameter}}}", route);
            var response = await client.SendAsync(AnyRequest(method, route), Ct);

            Assert.True(response.StatusCode == HttpStatusCode.NotFound,
                $"{method} {route} returned {(int)response.StatusCode} with an unknown link");
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
        var serie = await CreateSerieAsync(admin, format: 3);
        var gameId = await PlayGameAsync(admin, serie.SeriesId);

        var parts = action.Split(' ');
        var url = "/" + string.Format(parts[1], gameId, serie.SeriesId, serie.TeamA[0]);
        return (parts[0], url);
    }

    [Fact]
    public async Task Un_utilisateur_connecte_non_admin_organise_une_serie_et_saisit_les_resultats()
    {
        var client = Factory.CreateSignedInClient("7005");
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayGameAsync(client, serie.SeriesId);

        var response = await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "B" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Un_visiteur_non_connecte_ne_cree_pas_de_serie_ni_de_game_ni_de_resultat()
    {
        var organisateur = Factory.CreateSignedInClient("7006");
        var serie = await CreateSerieAsync(organisateur, format: 3);
        var gameId = await PlayGameAsync(organisateur, serie.SeriesId);
        var anonymous = Factory.CreateAnonymousClient();

        var create = await anonymous.PostAsJsonAsync("/series",
            new { teamA = serie.TeamA, teamB = serie.TeamB, format = 3 }, Ct);
        var result = await anonymous.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct);
        var next = await anonymous.PostAsJsonAsync($"/series/{serie.SeriesId}/games", new { teamASide = "red" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, create.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, next.StatusCode);
        var lue = await ReadJsonAsync(await organisateur.GetAsync($"/series/{serie.SeriesId}", Ct));
        Assert.Equal("pending", Assert.Single(lue.GetProperty("games").EnumerateArray()).GetProperty("status").GetString());
    }

    [Fact]
    public async Task Un_utilisateur_connecte_non_admin_arrete_une_serie()
    {
        var organisateur = Factory.CreateSignedInClient("7007");
        var serie = await CreateSerieAsync(organisateur, format: 3);
        await PlayAndWinAsync(organisateur, serie.SeriesId, "A");
        var autre = Factory.CreateSignedInClient("7008");

        var response = await autre.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpRequestMessage AdminRequest(string method, string url) =>
        new(new HttpMethod(method), url)
        {
            Content = method switch
            {
                "DELETE" => null,
                _ when url.EndsWith("/result") => JsonContent.Create(new { winner = "A" }),
                _ when url.EndsWith("/cost") => JsonContent.Create(new { cost = 3 }),
                _ when url.EndsWith("/peak") => JsonContent.Create(new { peakTier = "GOLD", peakDivision = 1, peakSeason = "S2025", peakLp = 10 }),
                _ => null
            }
        };
}
