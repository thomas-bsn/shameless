using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class RevancheTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Une_revanche_cree_une_nouvelle_serie_dans_la_meme_soiree_avec_les_memes_equipes()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/rematch",
            new { format = 3, fearless = true }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var revancheId = (await ReadJsonAsync(response)).GetProperty("seriesId").GetInt32();
        Assert.NotEqual(serie.SeriesId, revancheId);
        Assert.Equal($"/series/{revancheId}", response.Headers.Location?.OriginalString);

        var origine = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct));
        var revanche = await ReadJsonAsync(await client.GetAsync($"/series/{revancheId}", Ct));
        Assert.Equal(origine.GetProperty("soireeId").GetInt32(), revanche.GetProperty("soireeId").GetInt32());
        Assert.Equal(3, revanche.GetProperty("format").GetInt32());
        Assert.True(revanche.GetProperty("fearless").GetBoolean());
        Assert.Equal("EnCours", revanche.GetProperty("outcome").GetString());
        Assert.Equal((0, 0), Score(revanche));
        Assert.Empty(revanche.GetProperty("games").EnumerateArray());
        Assert.Equal(["A1", "A2", "A3", "A4", "A5"], Prenoms(revanche.GetProperty("teamA")));
        Assert.Equal(["B1", "B2", "B3", "B4", "B5"], Prenoms(revanche.GetProperty("teamB")));
    }

    [Fact]
    public async Task Une_revanche_apres_un_remplacement_reprend_la_derniere_composition()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var c1 = await CreatePlayerAsync(client, "C1");
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{serie.SeriesId}/replacements",
            new { replacedPlayerId = serie.TeamB[2], replacementPlayerId = c1 }, Ct));
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/rematch", new { format = 1 }, Ct);

        var revancheId = (await ReadJsonAsync(response)).GetProperty("seriesId").GetInt32();
        var revanche = await ReadJsonAsync(await client.GetAsync($"/series/{revancheId}", Ct));
        Assert.Equal(1, revanche.GetProperty("format").GetInt32());
        Assert.False(revanche.GetProperty("fearless").GetBoolean());
        Assert.Equal(["A1", "A2", "A3", "A4", "A5"], Prenoms(revanche.GetProperty("teamA")));
        Assert.Equal(["B1", "B2", "B4", "B5", "C1"], Prenoms(revanche.GetProperty("teamB")));

        await PlayAndWinAsync(client, revancheId, "B");
        var jouee = await ReadJsonAsync(await client.GetAsync($"/series/{revancheId}", Ct));
        var game = jouee.GetProperty("games").EnumerateArray().Single();
        Assert.Equal(["B1", "B2", "B4", "B5", "C1"], Prenoms(game.GetProperty("teamB")));
        Assert.Equal("Gagnee", jouee.GetProperty("outcome").GetString());
        Assert.Equal("B", jouee.GetProperty("winningTeam").GetString());
    }

    [Fact]
    public async Task Une_revanche_d_une_serie_nulle_est_possible()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 2);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayAndWinAsync(client, serie.SeriesId, "B");

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/rematch", new { format = 1 }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Une_revanche_d_une_serie_interrompue_est_possible()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));
        Assert.Equal("Interrompue", (await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct)))
            .GetProperty("outcome").GetString());

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/rematch", new { format = 3 }, Ct);

        var revancheId = (await ReadJsonAsync(response)).GetProperty("seriesId").GetInt32();
        var revanche = await ReadJsonAsync(await client.GetAsync($"/series/{revancheId}", Ct));
        Assert.Equal(["A1", "A2", "A3", "A4", "A5"], Prenoms(revanche.GetProperty("teamA")));
        Assert.Equal(["B1", "B2", "B3", "B4", "B5"], Prenoms(revanche.GetProperty("teamB")));
    }

    [Fact]
    public async Task Une_revanche_au_format_invalide_est_refusee()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/rematch", new { format = 4 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await AssertSeuleSerieAsync(client, serie.SeriesId);
    }

    [Fact]
    public async Task Une_revanche_d_une_serie_inconnue_repond_404()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);

        var response = await client.PostAsJsonAsync("/series/999999/rematch", new { format = 1 }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task La_revanche_est_publiee_dans_openapi()
    {
        await using var development = Factory.WithWebHostBuilder(b =>
            Microsoft.AspNetCore.Hosting.HostingAbstractionsWebHostBuilderExtensions.UseEnvironment(b, "Development"));

        var document = await ReadJsonAsync(await development.CreateClient().GetAsync("/openapi/v1.json", Ct));

        Assert.True(document.GetProperty("paths").GetProperty("/series/{id}/rematch").TryGetProperty("post", out _));
        Assert.True(document.GetProperty("components").GetProperty("schemas").TryGetProperty("RematchRequest", out _));
    }

    [Fact]
    public async Task Une_revanche_d_une_serie_en_cours_est_refusee()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/rematch", new { format = 3 }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await AssertSeuleSerieAsync(client, serie.SeriesId);
    }

    [Fact]
    public async Task Une_revanche_quand_la_soiree_de_la_serie_est_fermee_est_refusee()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        var soireeId = (await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct)))
            .GetProperty("soireeId").GetInt32();
        await ReadJsonAsync(await client.PostAsync($"/soirees/{soireeId}/close", null, Ct));

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/rematch", new { format = 1 }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await AssertSeuleSerieAsync(client, serie.SeriesId);
    }

    [Fact]
    public async Task Une_revanche_d_une_serie_d_une_soiree_precedente_est_refusee_meme_si_une_autre_soiree_est_ouverte()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        var soireeId = (await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct)))
            .GetProperty("soireeId").GetInt32();
        await ReadJsonAsync(await client.PostAsync($"/soirees/{soireeId}/close", null, Ct));
        await OpenSoireeAsync(client);

        var response = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/rematch", new { format = 1 }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        await AssertSeuleSerieAsync(client, serie.SeriesId);
    }

    private async Task AssertSeuleSerieAsync(HttpClient client, int seriesId)
    {
        var suivante = await client.GetAsync($"/series/{seriesId + 1}", Ct);
        Assert.Equal(HttpStatusCode.NotFound, suivante.StatusCode);
    }

    private static string?[] Prenoms(JsonElement team) =>
        team.EnumerateArray().Select(p => p.GetProperty("prenom").GetString()).Order().ToArray();

    private static (int, int) Score(JsonElement serie) =>
        (serie.GetProperty("teamAWins").GetInt32(), serie.GetProperty("teamBWins").GetInt32());
}
