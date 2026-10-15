using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class RemplacementTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Un_remplacement_entre_deux_games_s_applique_a_la_prochaine_game_sans_toucher_aux_games_jouees()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5);
        var c1 = await CreatePlayerAsync(client, "C1");
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayAndWinAsync(client, serie.SeriesId, "B");

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/replacements",
            new { replacedPlayerId = serie.TeamA[1], replacementPlayerId = c1 }, Ct);

        var remplacee = await ReadJsonAsync(response);
        Assert.Equal(["A1", "A3", "A4", "A5", "C1"], Prenoms(remplacee.GetProperty("teamA")));
        Assert.Equal(["B1", "B2", "B3", "B4", "B5"], Prenoms(remplacee.GetProperty("teamB")));
        Assert.Equal((1, 1), Score(remplacee));
        Assert.Equal("EnCours", remplacee.GetProperty("outcome").GetString());

        await PlayAndWinAsync(client, serie.SeriesId, "A");

        var result = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        var games = result.GetProperty("games").EnumerateArray().ToArray();
        Assert.Equal(["A1", "A2", "A3", "A4", "A5"], Prenoms(games[0].GetProperty("teamA")));
        Assert.Equal(["A1", "A2", "A3", "A4", "A5"], Prenoms(games[1].GetProperty("teamA")));
        Assert.Equal(["A1", "A3", "A4", "A5", "C1"], Prenoms(games[2].GetProperty("teamA")));
        Assert.Equal(["B1", "B2", "B3", "B4", "B5"], Prenoms(games[2].GetProperty("teamB")));
        Assert.Equal((2, 1), Score(result));
    }

    [Fact]
    public async Task Un_joueur_de_l_equipe_B_peut_etre_remplace_avant_la_premiere_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var c1 = await CreatePlayerAsync(client, "C1");

        await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{serie.SeriesId}/replacements",
            new { replacedPlayerId = serie.TeamB[4], replacementPlayerId = c1 }, Ct));
        var gameId = await PlayGameAsync(client, serie.SeriesId, "red");

        var result = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        var game = result.GetProperty("games").EnumerateArray().Single();
        Assert.Equal(gameId, game.GetProperty("id").GetInt32());
        Assert.Equal(["B1", "B2", "B3", "B4", "C1"], Prenoms(game.GetProperty("teamB")));
        Assert.Equal(["B1", "B2", "B3", "B4", "C1"], Prenoms(result.GetProperty("teamB")));
    }

    [Theory]
    [InlineData("remplaçant déjà dans son Équipe", 2, 409)]
    [InlineData("remplaçant déjà dans l'autre Équipe", 7, 409)]
    [InlineData("remplacé hors de la Série", -1, 400)]
    [InlineData("remplaçant inconnu", -2, 400)]
    public async Task Un_remplacement_incoherent_est_refuse(string cas, int remplacant, int status)
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var c1 = await CreatePlayerAsync(client, "C1");
        var all = serie.TeamA.Concat(serie.TeamB).ToArray();
        var (replacedPlayerId, replacementPlayerId) = remplacant switch
        {
            -1 => (c1, all[0]),
            -2 => (all[0], 999_999),
            _ => (all[0], all[remplacant])
        };

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/replacements",
            new { replacedPlayerId, replacementPlayerId }, Ct);

        Assert.True((int)response.StatusCode == status, cas);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var unchanged = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        Assert.Equal(["A1", "A2", "A3", "A4", "A5"], Prenoms(unchanged.GetProperty("teamA")));
        Assert.Equal(["B1", "B2", "B3", "B4", "B5"], Prenoms(unchanged.GetProperty("teamB")));
    }

    [Fact]
    public async Task Un_remplacement_sur_une_serie_terminee_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var c1 = await CreatePlayerAsync(client, "C1");
        await PlayAndWinAsync(client, serie.SeriesId, "B");

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/replacements",
            new { replacedPlayerId = serie.TeamA[0], replacementPlayerId = c1 }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var result = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        Assert.Equal(["A1", "A2", "A3", "A4", "A5"], Prenoms(result.GetProperty("teamA")));
        Assert.Equal((0, 1), Score(result));
    }

    [Fact]
    public async Task Un_remplacement_pendant_une_game_en_attente_de_resultat_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var c1 = await CreatePlayerAsync(client, "C1");
        await PlayGameAsync(client, serie.SeriesId);

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/replacements",
            new { replacedPlayerId = serie.TeamA[0], replacementPlayerId = c1 }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var result = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        Assert.Equal(["A1", "A2", "A3", "A4", "A5"], Prenoms(result.GetProperty("teamA")));
        Assert.Equal(["A1", "A2", "A3", "A4", "A5"], Prenoms(result.GetProperty("games")[0].GetProperty("teamA")));
    }

    [Fact]
    public async Task Un_remplacement_dans_une_serie_inconnue_repond_404()
    {
        var client = Factory.CreateSignedInClient();
        var ids = await CreatePlayersAsync(client, "A1", "C1");

        var response = await client.PostAsJsonAsync("/series/999999/replacements",
            new { replacedPlayerId = ids[0], replacementPlayerId = ids[1] }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Le_remplacement_est_publie_dans_openapi()
    {
        await using var development = Factory.WithWebHostBuilder(b =>
            Microsoft.AspNetCore.Hosting.HostingAbstractionsWebHostBuilderExtensions.UseEnvironment(b, "Development"));

        var document = await ReadJsonAsync(await development.CreateClient().GetAsync("/openapi/v1.json", Ct));

        Assert.True(document.GetProperty("paths").GetProperty("/series/{id}/replacements").TryGetProperty("post", out _));
        Assert.True(document.GetProperty("components").GetProperty("schemas").TryGetProperty("ReplacePlayerRequest", out _));
    }

    private static string?[] Prenoms(JsonElement team) =>
        team.EnumerateArray().Select(p => p.GetProperty("prenom").GetString()).Order().ToArray();

    private static (int, int) Score(JsonElement serie) =>
        (serie.GetProperty("teamAWins").GetInt32(), serie.GetProperty("teamBWins").GetInt32());
}
