using System.Net;
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
    public async Task Fermer_une_soiree_inexistante_repond_404()
    {
        var response = await Factory.CreateSignedInClient().PostAsync("/soirees/999/close", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Theory]
    [InlineData("POST", "/soirees")]
    [InlineData("GET", "/soirees/open")]
    [InlineData("POST", "/soirees/1/close")]
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
