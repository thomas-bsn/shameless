using System.Net;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>Arrêter une Série (ADR 0009) : l'annuler si aucune game n'est terminée, sinon l'interrompre.</summary>
public class ArretSerieTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Arreter_une_serie_sans_game_terminee_l_annule_et_l_efface_avec_ses_games_en_attente()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId);

        var arret = await ReadJsonAsync(await StopAsync(client, serie.SeriesId));

        Assert.Equal("Annulee", arret.GetProperty("arret").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/series/{serie.SeriesId}", Ct)).StatusCode);
        var pending = await ReadJsonAsync(await client.GetAsync("/games/pending", Ct));
        Assert.Empty(pending.EnumerateArray());
        var active = await ReadJsonAsync(await client.GetAsync("/series/active", Ct));
        Assert.Empty(active.GetProperty("series").EnumerateArray());
    }

    [Fact]
    public async Task Arreter_une_serie_avec_une_game_terminee_l_interrompt_sans_vainqueur_et_garde_le_cost()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayAndWinAsync(client, serie.SeriesId, "B");
        var costsAvant = await CostsAsync(client, serie);
        await PlayGameAsync(client, serie.SeriesId);

        var arret = await ReadJsonAsync(await StopAsync(client, serie.SeriesId));

        Assert.Equal("Interrompue", arret.GetProperty("arret").GetString());
        var lue = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        Assert.Equal("Interrompue", lue.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, lue.GetProperty("winningTeam").ValueKind);
        Assert.Equal((2, 1), (lue.GetProperty("teamAWins").GetInt32(), lue.GetProperty("teamBWins").GetInt32()));
        var games = lue.GetProperty("games").EnumerateArray().ToList();
        Assert.Equal(3, games.Count);
        Assert.All(games, g => Assert.Equal("completed", g.GetProperty("status").GetString()));
        Assert.Empty((await ReadJsonAsync(await client.GetAsync("/games/pending", Ct))).EnumerateArray());
        var active = await ReadJsonAsync(await client.GetAsync("/series/active", Ct));
        Assert.Empty(active.GetProperty("series").EnumerateArray());
        // Après 3 games (2 victoires, 1 défaite pour l'Équipe A) : l'ajustement de winrate est de ±3 ((2/3 - 50 %) × 150 × 3/20, tronqué).
        Assert.Equal((3, -3), (costsAvant.A.GetProperty("winrateAdjustment").GetInt32(), costsAvant.B.GetProperty("winrateAdjustment").GetInt32()));
        var costsApres = await CostsAsync(client, serie);
        Assert.Equal(costsAvant.A.ToString(), costsApres.A.ToString());
        Assert.Equal(costsAvant.B.ToString(), costsApres.B.ToString());
    }

    [Theory]
    [InlineData("gagnee", 1, new[] { "A" })]
    [InlineData("nulle", 2, new[] { "A", "B" })]
    [InlineData("interrompue", 3, new[] { "B" })]
    public async Task Arreter_une_serie_terminee_repond_409(string issue, int format, string[] winners)
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format);
        foreach (var winner in winners)
            await PlayAndWinAsync(client, serie.SeriesId, winner);
        if (issue == "interrompue")
            await ReadJsonAsync(await StopAsync(client, serie.SeriesId));
        var avant = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));

        var response = await StopAsync(client, serie.SeriesId);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        var apres = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        Assert.Equal(avant.ToString(), apres.ToString());
    }

    [Fact]
    public async Task Arreter_une_serie_inconnue_repond_404()
    {
        var client = Factory.CreateSignedInClient();

        var response = await StopAsync(client, 999);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Une_serie_sans_aucune_game_est_annulee()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);

        var arret = await ReadJsonAsync(await StopAsync(client, serie.SeriesId));

        Assert.Equal("Annulee", arret.GetProperty("arret").GetString());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/series/{serie.SeriesId}", Ct)).StatusCode);
    }

    [Fact]
    public async Task L_arret_de_serie_est_publie_dans_openapi_et_l_abandon_est_retire()
    {
        await using var development = Factory.WithWebHostBuilder(b =>
            Microsoft.AspNetCore.Hosting.HostingAbstractionsWebHostBuilderExtensions.UseEnvironment(b, "Development"));

        var document = await ReadJsonAsync(await development.CreateClient().GetAsync("/openapi/v1.json", Ct));

        var paths = document.GetProperty("paths");
        Assert.True(paths.GetProperty("/series/{id}/stop").TryGetProperty("post", out _));
        Assert.False(paths.TryGetProperty("/series/{id}/abandon", out _));
        Assert.True(document.GetProperty("components").GetProperty("schemas").TryGetProperty("StopSeriesResponse", out _));
    }

    private async Task<(JsonElement A, JsonElement B)> CostsAsync(HttpClient client, CreatedSerie serie) =>
        (await ReadJsonAsync(await client.GetAsync($"/players/{serie.TeamA[0]}/cost", Ct)),
         await ReadJsonAsync(await client.GetAsync($"/players/{serie.TeamB[0]}/cost", Ct)));

    private static Task<HttpResponseMessage> StopAsync(HttpClient client, int seriesId) =>
        client.PostAsync($"/series/{seriesId}/stop", null, Ct);
}
