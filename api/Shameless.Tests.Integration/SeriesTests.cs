using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class SeriesTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Un_utilisateur_connecte_cree_une_serie_dans_la_soiree_ouverte()
    {
        var client = Factory.CreateSignedInClient();
        var soireeId = await OpenSoireeAsync(client);
        var ids = await CreatePlayersAsync(client, "A1", "A2", "A3", "A4", "A5", "B1", "B2", "B3", "B4", "B5");

        var response = await client.PostAsJsonAsync("/series",
            new { teamA = ids[..5], teamB = ids[5..], format = 2, fearless = true }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await ReadJsonAsync(response);
        var seriesId = created.GetProperty("seriesId").GetInt32();
        var serie = await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));
        Assert.Equal(soireeId, serie.GetProperty("soireeId").GetInt32());
        Assert.Equal(2, serie.GetProperty("format").GetInt32());
        Assert.True(serie.GetProperty("fearless").GetBoolean());
        Assert.False(serie.GetProperty("alternateSides").GetBoolean());
        Assert.Equal("EnCours", serie.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, serie.GetProperty("winningTeam").ValueKind);
        Assert.Equal(["A1", "A2", "A3", "A4", "A5"], Prenoms(serie.GetProperty("teamA")));
        Assert.Equal(["B1", "B2", "B3", "B4", "B5"], Prenoms(serie.GetProperty("teamB")));
        Assert.Empty(serie.GetProperty("games").EnumerateArray());
    }

    [Theory]
    [InlineData(1, new[] { "A" }, "A")]
    [InlineData(2, new[] { "B", "B" }, "B")]
    [InlineData(3, new[] { "A", "B", "A" }, "A")]
    [InlineData(5, new[] { "B", "A", "B", "A", "B" }, "B")]
    public async Task Une_serie_est_gagnee_quand_une_equipe_atteint_les_victoires_du_format(
        int format, string[] winners, string expectedWinner)
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format);

        foreach (var winner in winners[..^1])
        {
            await PlayAndWinAsync(client, serie.SeriesId, winner);
            var enCours = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
            Assert.Equal("EnCours", enCours.GetProperty("outcome").GetString());
        }
        await PlayAndWinAsync(client, serie.SeriesId, winners[^1]);

        var result = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        Assert.Equal("Gagnee", result.GetProperty("outcome").GetString());
        Assert.Equal(expectedWinner, result.GetProperty("winningTeam").GetString());
        Assert.Equal(winners.Count(w => w == "A"), result.GetProperty("teamAWins").GetInt32());
        Assert.Equal(winners.Count(w => w == "B"), result.GetProperty("teamBWins").GetInt32());
        Assert.Equal(winners.Length, result.GetProperty("games").GetArrayLength());
        var active = await ReadJsonAsync(await client.GetAsync("/series/active", Ct));
        Assert.Empty(active.GetProperty("series").EnumerateArray());
    }

    [Fact]
    public async Task Un_BO2_a_1_1_est_nul()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 2);

        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayAndWinAsync(client, serie.SeriesId, "B");

        var result = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        Assert.Equal("Nulle", result.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, result.GetProperty("winningTeam").ValueKind);
        Assert.Equal((1, 1), (result.GetProperty("teamAWins").GetInt32(), result.GetProperty("teamBWins").GetInt32()));
    }

    [Theory]
    [InlineData(1, new[] { "A" })]
    [InlineData(2, new[] { "A", "B" })]
    [InlineData(3, new[] { "B", "B" })]
    public async Task Une_serie_terminee_refuse_une_game_de_trop(int format, string[] winners)
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format);
        foreach (var winner in winners)
            await PlayAndWinAsync(client, serie.SeriesId, winner);

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/games", new { teamASide = "blue" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Une_game_dont_le_resultat_est_saisi_ne_peut_pas_etre_resaisie()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");

        var response = await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "B" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var result = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        Assert.Equal((1, 0), (result.GetProperty("teamAWins").GetInt32(), result.GetProperty("teamBWins").GetInt32()));
    }

    [Fact]
    public async Task Une_seule_game_attend_son_resultat_a_la_fois()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId);

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/games", new { teamASide = "red" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Le_side_de_chaque_game_se_choisit_a_la_main_et_le_score_suit_les_equipes()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);

        await PlayAndWinAsync(client, serie.SeriesId, "B", teamASide: "red");
        await PlayAndWinAsync(client, serie.SeriesId, "A", teamASide: "red");
        await PlayAndWinAsync(client, serie.SeriesId, "A", teamASide: "blue");

        var result = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        var games = result.GetProperty("games").EnumerateArray().ToList();
        Assert.Equal([("red", "blue", "B"), ("red", "blue", "A"), ("blue", "red", "A")], games.Select(Sides));
        Assert.All(games, g =>
        {
            Assert.Equal(["A1", "A2", "A3", "A4", "A5"], Prenoms(g.GetProperty("teamA")));
            Assert.Equal(["B1", "B2", "B3", "B4", "B5"], Prenoms(g.GetProperty("teamB")));
        });
        Assert.Equal("A", result.GetProperty("winningTeam").GetString());

        var pending = await ReadJsonAsync(await client.GetAsync("/games/pending", Ct));
        Assert.Empty(pending.EnumerateArray());
    }

    [Fact]
    public async Task Sans_alternance_le_side_de_la_game_est_obligatoire()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await RejoindreLesCapitainesAsync(client, serie.SeriesId);

        var missing = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/games", new { teamASide = (string?)null }, Ct);
        var invalid = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/games", new { teamASide = "A" }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal("application/problem+json", invalid.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Avec_l_alternance_automatique_les_sides_s_inversent_a_chaque_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5, alternateSides: true);

        await PlayAndWinAsync(client, serie.SeriesId, "A", teamASide: null);
        await PlayAndWinAsync(client, serie.SeriesId, "B", teamASide: null);
        await PlayAndWinAsync(client, serie.SeriesId, "A", teamASide: null);
        var pendingGame = await ReadJsonAsync(await client.PostAsJsonAsync(
            $"/series/{serie.SeriesId}/games", new { teamASide = (string?)null }, Ct));
        Assert.Equal("red", pendingGame.GetProperty("teamASide").GetString());

        var result = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        Assert.True(result.GetProperty("alternateSides").GetBoolean());
        Assert.Equal(["blue", "red", "blue", "red"],
            result.GetProperty("games").EnumerateArray().Select(g => g.GetProperty("teamASide").GetString()));
    }

    [Fact]
    public async Task Avec_l_alternance_un_side_choisi_a_la_main_l_emporte_et_l_alternance_repart_de_lui()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5, alternateSides: true);

        await PlayAndWinAsync(client, serie.SeriesId, "A", teamASide: "red");
        await PlayAndWinAsync(client, serie.SeriesId, "A", teamASide: null);

        var result = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        Assert.Equal(["red", "blue"],
            result.GetProperty("games").EnumerateArray().Select(g => g.GetProperty("teamASide").GetString()));
    }

    [Fact]
    public async Task Creer_une_serie_sans_soiree_ouverte_repond_409()
    {
        var client = Factory.CreateSignedInClient();
        var ids = await CreatePlayersAsync(client, "A1", "A2", "A3", "A4", "A5", "B1", "B2", "B3", "B4", "B5");

        var response = await client.PostAsJsonAsync("/series",
            new { teamA = ids[..5], teamB = ids[5..], format = 3 }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    public static TheoryData<string, int> CommandesInvalides() => new()
    {
        { "quatre joueurs en A", 3 },
        { "un joueur dans les deux équipes", 3 },
        { "un joueur en double", 3 },
        { "un joueur inconnu", 3 },
        { "format", 4 },
        { "format", 0 },
        { "format", 7 }
    };

    [Theory]
    [MemberData(nameof(CommandesInvalides))]
    public async Task Une_serie_invalide_est_refusee_en_400(string cas, int format)
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var ids = await CreatePlayersAsync(client, "A1", "A2", "A3", "A4", "A5", "B1", "B2", "B3", "B4", "B5");
        int[] teamA = ids[..5], teamB = ids[5..];
        switch (cas)
        {
            case "quatre joueurs en A": teamA = ids[..4]; break;
            case "un joueur dans les deux équipes": teamB = [ids[0], .. ids[6..]]; break;
            case "un joueur en double": teamA = [ids[0], ids[0], .. ids[2..5]]; break;
            case "un joueur inconnu": teamA = [999, .. ids[1..5]]; break;
        }

        var response = await client.PostAsJsonAsync("/series", new { teamA, teamB, format }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var active = await ReadJsonAsync(await client.GetAsync("/series/active", Ct));
        Assert.Empty(active.GetProperty("series").EnumerateArray());
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("C")]
    [InlineData("")]
    public async Task Un_vainqueur_qui_n_est_pas_une_equipe_est_refuse(string winner)
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayGameAsync(client, serie.SeriesId);

        var response = await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Les_series_en_cours_sont_listees_comme_actives()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        var active = await ReadJsonAsync(await client.GetAsync("/series/active", Ct));

        var enCours = Assert.Single(active.GetProperty("series").EnumerateArray());
        Assert.Equal(serie.SeriesId, enCours.GetProperty("seriesId").GetInt32());
        Assert.Equal(1, enCours.GetProperty("teamAWins").GetInt32());
    }

    [Fact]
    public async Task Le_contrat_des_series_est_publie_dans_openapi()
    {
        await using var development = Factory.WithWebHostBuilder(b =>
            Microsoft.AspNetCore.Hosting.HostingAbstractionsWebHostBuilderExtensions.UseEnvironment(b, "Development"));

        var document = await ReadJsonAsync(await development.CreateClient().GetAsync("/openapi/v1.json", Ct));

        var paths = document.GetProperty("paths");
        Assert.True(paths.GetProperty("/series").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/series/{id}/games").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/games/{id}/result").TryGetProperty("put", out _));
        Assert.False(paths.TryGetProperty("/games", out _));
        var schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty("CreateSeriesRequest", out _));
        Assert.True(schemas.TryGetProperty("SeriesResponse", out _));
    }

    private static string?[] Prenoms(JsonElement team) =>
        team.EnumerateArray().Select(p => p.GetProperty("prenom").GetString()).Order().ToArray();

    private static (string?, string?, string?) Sides(JsonElement game) =>
        (game.GetProperty("teamASide").GetString(),
         game.GetProperty("teamBSide").GetString(),
         game.GetProperty("winner").GetString());
}
