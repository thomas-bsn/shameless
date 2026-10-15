using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class SoireeTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Un_utilisateur_connecte_ouvre_une_soiree_et_la_relit_comme_soiree_ouverte()
    {
        var client = Factory.CreateSignedInClient();

        var opened = await client.PostAsync("/soirees", null, Ct);
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);
        var soiree = await ReadJsonAsync(opened);
        Assert.Equal("Open", soiree.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, soiree.GetProperty("closedAt").ValueKind);

        var current = await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct));
        var lue = current.GetProperty("soiree");
        Assert.Equal(soiree.GetProperty("id").GetInt32(), lue.GetProperty("id").GetInt32());
        Assert.Equal("Open", lue.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Sans_soiree_ouverte_la_lecture_renvoie_une_soiree_vide_explicite()
    {
        var client = Factory.CreateSignedInClient();

        var current = await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct));

        Assert.True(current.TryGetProperty("soiree", out var soiree));
        Assert.Equal(JsonValueKind.Null, soiree.ValueKind);
    }

    [Fact]
    public async Task Ouvrir_une_deuxieme_soiree_repond_409()
    {
        var client = Factory.CreateSignedInClient();
        await ReadJsonAsync(await client.PostAsync("/soirees", null, Ct));

        var second = await Factory.CreateSignedInClient("1002").PostAsync("/soirees", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Des_ouvertures_simultanees_ne_creent_qu_une_seule_soiree()
    {
        var client = Factory.CreateSignedInClient();

        var responses = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => client.PostAsync("/soirees", null, Ct)));

        Assert.Single(responses, r => r.StatusCode == HttpStatusCode.Created);
        Assert.All(responses.Where(r => r.StatusCode != HttpStatusCode.Created),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task Fermer_la_soiree_ouverte_la_retire_et_permet_d_en_ouvrir_une_autre()
    {
        var client = Factory.CreateSignedInClient();
        var soiree = await ReadJsonAsync(await client.PostAsync("/soirees", null, Ct));
        var id = soiree.GetProperty("id").GetInt32();

        var closed = await ReadJsonAsync(await client.PostAsync($"/soirees/{id}/close", null, Ct));

        Assert.Equal("Closed", closed.GetProperty("status").GetString());
        Assert.NotEqual(JsonValueKind.Null, closed.GetProperty("closedAt").ValueKind);
        var current = await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct));
        Assert.Equal(JsonValueKind.Null, current.GetProperty("soiree").ValueKind);
        var reopened = await client.PostAsync("/soirees", null, Ct);
        Assert.Equal(HttpStatusCode.Created, reopened.StatusCode);
    }

    [Fact]
    public async Task Une_soiree_peut_passer_minuit_et_garde_ses_heures_d_ouverture_et_de_fermeture()
    {
        var client = Factory.CreateSignedInClient();
        var ouverture = Factory.Clock.GetUtcNow().AddMinutes(1);
        Factory.Clock.SetUtcNow(ouverture);
        var id = (await ReadJsonAsync(await client.PostAsync("/soirees", null, Ct))).GetProperty("id").GetInt32();

        Factory.Clock.Advance(TimeSpan.FromHours(4));
        var closed = await ReadJsonAsync(await client.PostAsync($"/soirees/{id}/close", null, Ct));

        Assert.Equal(ouverture.UtcDateTime, closed.GetProperty("openedAt").GetDateTime(), TimeSpan.FromMilliseconds(1));
        Assert.Equal(ouverture.UtcDateTime.AddHours(4), closed.GetProperty("closedAt").GetDateTime(), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Fermer_une_soiree_deja_fermee_repond_409()
    {
        var client = Factory.CreateSignedInClient();
        var id = (await ReadJsonAsync(await client.PostAsync("/soirees", null, Ct))).GetProperty("id").GetInt32();
        await ReadJsonAsync(await client.PostAsync($"/soirees/{id}/close", null, Ct));

        var again = await client.PostAsync($"/soirees/{id}/close", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("application/problem+json", again.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Fermer_une_soiree_avec_une_serie_en_cours_repond_409_et_la_laisse_ouverte()
    {
        var client = Factory.CreateSignedInClient();
        var id = await OpenSoireeAsync(client);
        await CreateSerieAsync(client);

        var response = await client.PostAsync($"/soirees/{id}/close", null, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Contains("arrêtez", problem.GetProperty("detail").GetString());
        var open = await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct));
        Assert.Equal(id, open.GetProperty("soiree").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Fermer_une_soiree_inexistante_repond_404()
    {
        var response = await Factory.CreateSignedInClient().PostAsync("/soirees/999/close", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Les_series_d_une_soiree_se_lisent_avec_leur_issue_de_la_plus_ancienne_a_la_plus_recente()
    {
        var client = Factory.CreateSignedInClient();
        var gagnee = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, gagnee.SeriesId, "B");
        var revanche = await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{gagnee.SeriesId}/rematch", new { format = 3 }, Ct));
        var soireeId = (await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct))).GetProperty("soiree").GetProperty("id").GetInt32();

        var lues = await ReadJsonAsync(await client.GetAsync($"/soirees/{soireeId}/series", Ct));

        var series = lues.GetProperty("series").EnumerateArray().ToList();
        Assert.Equal([gagnee.SeriesId, revanche.GetProperty("seriesId").GetInt32()],
            series.Select(s => s.GetProperty("seriesId").GetInt32()));
        Assert.Equal("Gagnee", series[0].GetProperty("outcome").GetString());
        Assert.Equal("B", series[0].GetProperty("winningTeam").GetString());
        Assert.Equal((0, 1), (series[0].GetProperty("teamAWins").GetInt32(), series[0].GetProperty("teamBWins").GetInt32()));
        Assert.Equal("EnCours", series[1].GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Les_series_d_une_autre_soiree_ne_sont_pas_listees()
    {
        var client = Factory.CreateSignedInClient();
        var ancienne = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, ancienne.SeriesId, "A");
        var ancienneId = (await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct))).GetProperty("soiree").GetProperty("id").GetInt32();
        await ReadJsonAsync(await client.PostAsync($"/soirees/{ancienneId}/close", null, Ct));
        var nouvelleId = await OpenSoireeAsync(client);

        var lues = await ReadJsonAsync(await client.GetAsync($"/soirees/{nouvelleId}/series", Ct));

        Assert.Empty(lues.GetProperty("series").EnumerateArray());
    }

    [Fact]
    public async Task Les_series_d_une_soiree_inexistante_repondent_404()
    {
        var response = await Factory.CreateSignedInClient().GetAsync("/soirees/999/series", Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("POST", "/soirees")]
    [InlineData("GET", "/soirees/open")]
    [InlineData("POST", "/soirees/1/close")]
    [InlineData("GET", "/soirees/1/series")]
    public async Task Un_visiteur_non_connecte_est_refuse(string method, string url)
    {
        var response = await Factory.CreateAnonymousClient().SendAsync(new HttpRequestMessage(new HttpMethod(method), url), Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Le_contrat_de_la_soiree_est_publie_dans_openapi()
    {
        await using var development = Factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));

        var document = await ReadJsonAsync(await development.CreateClient().GetAsync("/openapi/v1.json", Ct));

        var paths = document.GetProperty("paths");
        Assert.True(paths.GetProperty("/soirees").TryGetProperty("post", out _));
        Assert.True(paths.GetProperty("/soirees/open").TryGetProperty("get", out _));
        Assert.True(paths.GetProperty("/soirees/{id}/close").TryGetProperty("post", out _));
        var schemas = document.GetProperty("components").GetProperty("schemas");
        Assert.True(schemas.TryGetProperty("SoireeResponse", out _));
        Assert.True(schemas.TryGetProperty("OpenSoireeResponse", out _));
    }
}
