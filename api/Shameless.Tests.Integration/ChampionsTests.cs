using System.Net;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class ChampionsTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Le_catalogue_des_champions_liste_id_key_nom_et_image()
    {
        var client = Factory.CreateSignedInClient();

        var champions = await ReadJsonAsync(await client.GetAsync("/champions", Ct));

        var lus = champions.EnumerateArray()
            .Select(c => (
                c.GetProperty("id").GetString(),
                c.GetProperty("key").GetString(),
                c.GetProperty("name").GetString(),
                c.GetProperty("imageUrl").GetString()))
            .ToList();
        Assert.Equal(25, lus.Count);
        Assert.Contains(("LeeSin", "64", "Lee Sin", "https://ddragon.test/img/champion/LeeSin.png"), lus);
    }

    [Fact]
    public async Task Le_catalogue_indisponible_repond_503()
    {
        Factory.Champions.Indisponible = true;

        var response = await Factory.CreateAnonymousClient().GetAsync("/champions", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Le_catalogue_des_champions_se_lit_sans_session()
    {
        var anonymous = Factory.CreateAnonymousClient();

        var response = await anonymous.GetAsync("/champions", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(25, (await ReadJsonAsync(response)).GetArrayLength());
    }
}
