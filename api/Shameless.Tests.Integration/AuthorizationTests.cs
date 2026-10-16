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
        "GET health",
        "GET champions"
    ];

    public static TheoryData<string> AdminActions() =>
    [
        "PUT players/{2}/cost",
        "POST players/{2}/peak",
        "DELETE players/{2}/peak"
    ];

    /// <summary>Comment une route s'ouvre sans session.</summary>
    private enum Ouverture { Session, Lien, CodeCompanion, Bot }

    private static Ouverture OuvertureDe(RouteEndpoint e) =>
        e.Metadata.GetMetadata<AccesParLienAttribute>() != null ? Ouverture.Lien
        : e.Metadata.GetMetadata<AccesParCodeCompanionAttribute>() != null ? Ouverture.CodeCompanion
        : e.Metadata.GetMetadata<AccesParBotAttribute>() != null ? Ouverture.Bot
        : Ouverture.Session;

    /// <summary>
    /// Les routes de l'API ouvertes de cette façon : par session, par lien d'accès (ADR 0010), par
    /// code Companion ou par la clé du bot (ADR 0013).
    /// </summary>
    private List<(string Method, string Route)> Endpoints(Ouverture ouverture) =>
        Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => OuvertureDe(e) == ouverture)
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
    public async Task Toute_route_hors_connexion_callback_sante_liens_d_acces_et_code_companion_demande_une_session()
    {
        var client = Factory.CreateAnonymousClient();
        var endpoints = Endpoints(Ouverture.Session);

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
        var endpoints = Endpoints(Ouverture.Lien);

        Assert.NotEmpty(endpoints);
        foreach (var (method, route) in endpoints)
        {
            Assert.Contains($"{{{AccesParLienAttribute.RouteParameter}}}", route);
            var response = await client.SendAsync(AnyRequest(method, route), Ct);

            Assert.True(response.StatusCode == HttpStatusCode.NotFound,
                $"{method} {route} returned {(int)response.StatusCode} with an unknown link");
        }
    }

    [Fact]
    public async Task Sans_session_une_route_ouverte_au_companion_refuse_un_code_inconnu()
    {
        var client = Factory.CreateAnonymousClient();
        var endpoints = Endpoints(Ouverture.CodeCompanion);

        Assert.NotEmpty(endpoints);
        foreach (var (method, route) in endpoints)
        {
            Assert.Contains($"{{{AccesParCodeCompanionAttribute.RouteParameter}}}", route);
            var response = await client.SendAsync(AnyRequest(method, route), Ct);

            Assert.True(response.StatusCode == HttpStatusCode.NotFound,
                $"{method} {route} returned {(int)response.StatusCode} with an unknown code");
        }
    }

    [Fact]
    public async Task Une_route_du_bot_est_sous_bot_et_refuse_sans_la_cle_du_bot_meme_avec_une_session()
    {
        var anonyme = Factory.CreateAnonymousClient();
        var connecte = Factory.CreateSignedInClient();
        var endpoints = Endpoints(Ouverture.Bot);

        Assert.NotEmpty(endpoints);
        foreach (var (method, route) in endpoints)
        {
            Assert.StartsWith("bot/", route);
            foreach (var client in new[] { anonyme, connecte })
            {
                var response = await client.SendAsync(AnyRequest(method, route), Ct);

                Assert.True(response.StatusCode == HttpStatusCode.Unauthorized,
                    $"{method} {route} returned {(int)response.StatusCode} without the bot key");
            }
        }
    }

    [Fact]
    public void Toute_route_sous_bot_est_ouverte_par_la_cle_du_bot()
    {
        var routes = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.RoutePattern.RawText!.TrimStart('/').StartsWith("bot/"))
            .ToList();

        Assert.NotEmpty(routes);
        Assert.All(routes, e => Assert.Equal(Ouverture.Bot, OuvertureDe(e)));
    }

    [Fact]
    public async Task Le_lien_spectateur_est_refuse_en_403_sur_toute_commande_de_capitaine()
    {
        var organisateur = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(organisateur);
        var liens = await ReadJsonAsync(await organisateur.PostAsync($"/series/{serie.SeriesId}/liens/envoyes", null, Ct));
        var spectateur = Uri.EscapeDataString(liens.GetProperty("lienSpectateur").GetString()!);
        var commandes = Factory.Services.GetRequiredService<EndpointDataSource>().Endpoints
            .OfType<RouteEndpoint>()
            .Where(e => e.Metadata.GetMetadata<AccesParLienAttribute>() is { CapitaineRequis: true })
            .SelectMany(e => (e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? ["GET"])
                .Select(method => (Method: method, Route: e.RoutePattern.RawText!.TrimStart('/'))))
            .ToList();
        var anonymous = Factory.CreateAnonymousClient();

        Assert.NotEmpty(commandes);
        foreach (var (method, route) in commandes)
        {
            var request = AnyRequest(method, route.Replace($"{{{AccesParLienAttribute.RouteParameter}}}", spectateur));
            var response = await anonymous.SendAsync(request, Ct);

            Assert.True(response.StatusCode == HttpStatusCode.Forbidden,
                $"{method} {route} returned {(int)response.StatusCode} with a spectator link");
        }
    }

    [Theory]
    [MemberData(nameof(AdminActions))]
    public async Task Un_joueur_non_admin_est_refuse_sur_les_actions_admin(string action)
    {
        var (method, url) = await PrepareAdminActionAsync(action);
        var client = Factory.CreateSignedInClient("7001");

        var response = await client.SendAsync(AdminRequest(method, url), Ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(AdminActions))]
    public async Task Un_admin_peut_faire_les_actions_admin(string action)
    {
        var (method, url) = await PrepareAdminActionAsync(action);
        var client = Factory.CreateAdminClient();

        var response = await client.SendAsync(AdminRequest(method, url), Ct);

        Assert.True(response.IsSuccessStatusCode, $"{method} {url} returned {(int)response.StatusCode}");
    }

    [Fact]
    public async Task Me_indique_si_l_utilisateur_connecte_est_admin()
    {
        var player = await Factory.CreateSignedInClient("7002").GetFromJsonAsync<JsonElement>("/auth/me", Ct);
        var admin = await Factory.CreateAdminClient().GetFromJsonAsync<JsonElement>("/auth/me", Ct);

        Assert.False(player.GetProperty("isAdmin").GetBoolean());
        Assert.True(admin.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Un_admin_fixe_le_cost_et_le_peak_d_un_joueur()
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
    public async Task Un_cost_hors_de_1_a_6_est_refuse(int cost)
    {
        var admin = Factory.CreateAdminClient();
        var playerId = await CreatePlayerAsync(admin, "Cible");

        var response = await admin.PutAsJsonAsync($"/players/{playerId}/cost", new { cost }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Lier_un_joueur_deja_lie_a_quelqu_un_d_autre_est_refuse_sauf_admin()
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
