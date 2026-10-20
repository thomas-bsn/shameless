using System.Net;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>Le catalogue des objets, sorts d'invocateur et runes (<c>GET /catalogue</c>) que le site lit pour afficher noms et icônes.</summary>
public class CatalogueDeJeuTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Le_catalogue_liste_objets_sorts_et_arbres_de_runes_avec_noms_et_icones()
    {
        var catalogue = await ReadJsonAsync(await Factory.CreateSignedInClient().GetAsync("/catalogue", Ct));

        Assert.Equal("15.1.1", catalogue.GetProperty("version").GetString());
        var ludens = catalogue.GetProperty("objets").EnumerateArray().Single(o => o.GetProperty("id").GetInt32() == 6655);
        Assert.Equal("Rayon de Luden", ludens.GetProperty("name").GetString());
        Assert.Equal("https://ddragon.test/cdn/15.1.1/img/item/6655.png", ludens.GetProperty("iconUrl").GetString());
        var flash = catalogue.GetProperty("sorts").EnumerateArray().Single(s => s.GetProperty("id").GetInt32() == 4);
        Assert.Equal("Saut éclair", flash.GetProperty("name").GetString());
        var domination = catalogue.GetProperty("arbres").EnumerateArray().Single();
        Assert.Equal(8100, domination.GetProperty("id").GetInt32());
        Assert.Equal("Électrocution", domination.GetProperty("runes")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Le_catalogue_se_lit_sans_session()
    {
        var response = await Factory.CreateAnonymousClient().GetAsync("/catalogue", Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Le_catalogue_indisponible_repond_503()
    {
        Factory.CatalogueDeJeu.Indisponible = true;

        var response = await Factory.CreateAnonymousClient().GetAsync("/catalogue", Ct);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }
}
