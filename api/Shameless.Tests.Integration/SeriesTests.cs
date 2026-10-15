using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class SeriesTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Le_score_d_un_BO3_suit_les_equipes_a_travers_les_changements_de_side()
    {
        var client = Factory.CreateAdminClient();
        var ids = await CreatePlayersAsync(client, "A1", "A2", "B1", "B2");
        int[] equipeA = ids[..2], equipeB = ids[2..];

        var first = await ReadJsonAsync(await client.PostAsJsonAsync("/games",
            new { blueTeam = equipeA, redTeam = equipeB, boFormat = 3 }, Ct));
        var seriesId = first.GetProperty("seriesId").GetInt32();

        var series = await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));
        Assert.Equal(3, series.GetProperty("format").GetInt32());
        Assert.False(series.TryGetProperty("teamAReference", out _));
        Assert.Equal(["A1", "A2"], Prenoms(series.GetProperty("teamA")));
        Assert.Equal(["B1", "B2"], Prenoms(series.GetProperty("teamB")));

        var result = await ReadJsonAsync(await client.PutAsJsonAsync(
            $"/games/{first.GetProperty("gameId").GetInt32()}/result", new { winner = "A" }, Ct));
        Assert.Equal("A", result.GetProperty("winner").GetString());

        var active = await ReadJsonAsync(await client.GetAsync("/series/active", Ct));
        var activeSeries = Assert.Single(active.GetProperty("series").EnumerateArray());
        Assert.Equal(seriesId, activeSeries.GetProperty("seriesId").GetInt32());
        Assert.Equal(1, activeSeries.GetProperty("teamAWins").GetInt32());

        // Game 2 : l'Équipe A passe côté rouge et gagne
        var second = await ReadJsonAsync(await client.PostAsJsonAsync("/games",
            new { blueTeam = equipeB, redTeam = equipeA, seriesId }, Ct));
        Assert.Equal(seriesId, second.GetProperty("seriesId").GetInt32());
        await ReadJsonAsync(await client.PutAsJsonAsync(
            $"/games/{second.GetProperty("gameId").GetInt32()}/result", new { winner = "A" }, Ct));

        series = await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));
        Assert.Equal(2, series.GetProperty("teamAWins").GetInt32());
        Assert.Equal(0, series.GetProperty("teamBWins").GetInt32());

        var games = series.GetProperty("games").EnumerateArray().ToList();
        Assert.Equal(2, games.Count);
        Assert.Equal(("blue", "red", "A"), Sides(games[0]));
        Assert.Equal(("red", "blue", "A"), Sides(games[1]));
        Assert.Equal(["A1", "A2"], Prenoms(games[1].GetProperty("teamA")));
        Assert.Equal(["B1", "B2"], Prenoms(games[1].GetProperty("teamB")));

        active = await ReadJsonAsync(await client.GetAsync("/series/active", Ct));
        Assert.Empty(active.GetProperty("series").EnumerateArray());
    }

    [Fact]
    public async Task L_equipe_B_gagne_cote_bleu_apres_un_changement_de_side()
    {
        var client = Factory.CreateAdminClient();
        var ids = await CreatePlayersAsync(client, "A1", "B1");

        var first = await ReadJsonAsync(await client.PostAsJsonAsync("/games",
            new { blueTeam = new[] { ids[0] }, redTeam = new[] { ids[1] }, boFormat = 3 }, Ct));
        var seriesId = first.GetProperty("seriesId").GetInt32();
        await ReadJsonAsync(await client.PutAsJsonAsync(
            $"/games/{first.GetProperty("gameId").GetInt32()}/result", new { winner = "A" }, Ct));

        var second = await ReadJsonAsync(await client.PostAsJsonAsync("/games",
            new { blueTeam = new[] { ids[1] }, redTeam = new[] { ids[0] }, seriesId }, Ct));
        await ReadJsonAsync(await client.PutAsJsonAsync(
            $"/games/{second.GetProperty("gameId").GetInt32()}/result", new { winner = "B" }, Ct));

        var series = await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));
        Assert.Equal(1, series.GetProperty("teamAWins").GetInt32());
        Assert.Equal(1, series.GetProperty("teamBWins").GetInt32());
        var game = series.GetProperty("games").EnumerateArray().Last();
        Assert.Equal(("red", "blue", "B"), Sides(game));
    }

    [Theory]
    [InlineData("blue")]
    [InlineData("C")]
    [InlineData("")]
    public async Task Un_vainqueur_qui_n_est_pas_une_equipe_est_refuse(string winner)
    {
        var client = Factory.CreateAdminClient();
        var ids = await CreatePlayersAsync(client, "A1", "B1");
        var game = await ReadJsonAsync(await client.PostAsJsonAsync("/games",
            new { blueTeam = new[] { ids[0] }, redTeam = new[] { ids[1] }, boFormat = 1 }, Ct));

        var response = await client.PutAsJsonAsync(
            $"/games/{game.GetProperty("gameId").GetInt32()}/result", new { winner }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Abandoning_a_series_deletes_it_with_its_games()
    {
        var client = Factory.CreateAdminClient();
        var ids = await CreatePlayersAsync(client, "X", "Y");
        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/games",
            new { blueTeam = new[] { ids[0] }, redTeam = new[] { ids[1] }, boFormat = 5 }, Ct));
        var seriesId = created.GetProperty("seriesId").GetInt32();

        var abandon = await ReadJsonAsync(await client.PutAsync($"/series/{seriesId}/abandon", null, Ct));
        Assert.True(abandon.GetProperty("success").GetBoolean());

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/series/{seriesId}", Ct)).StatusCode);
        Assert.Empty((await ReadJsonAsync(await client.GetAsync("/games/pending", Ct))).EnumerateArray());
    }

    [Fact]
    public async Task Abandoning_an_unknown_series_returns_404()
    {
        var client = Factory.CreateAdminClient();

        var response = await client.PutAsync("/series/999/abandon", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static string?[] Prenoms(JsonElement team) =>
        team.EnumerateArray().Select(p => p.GetProperty("prenom").GetString()).Order().ToArray();

    private static (string?, string?, string?) Sides(JsonElement game) =>
        (game.GetProperty("teamASide").GetString(),
         game.GetProperty("teamBSide").GetString(),
         game.GetProperty("winner").GetString());
}
