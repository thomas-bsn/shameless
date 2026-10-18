using System.Net;
using System.Text;
using Shameless.Application.Interfaces;
using Shameless.Infrastructure.Integrations.DataDragon;

namespace Shameless.Tests.Integration.Integrations;

/// <summary>Le catalogue Data Dragon face à un Data Dragon simulé : toute panne rend le catalogue indisponible.</summary>
public class DataDragonChampionCatalogTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Versions = """["15.1.1"]""";
    private const string Champions = """{"data":{"Ahri":{"id":"Ahri","key":"103","name":"Ahri","image":{"full":"Ahri.png"}}}}""";

    private const string MerakiPath = "/riot/lol/resources/latest/en-US/champions.json";

    // Ahri (103) mid ; Teemo (17) top et support ; Zyra (143) absent de Meraki.
    private const string Meraki = """
        {"Ahri":{"id":103,"positions":["MIDDLE"]},
         "Teemo":{"id":17,"positions":["TOP","SUPPORT"]},
         "Jinx":{"id":222,"positions":["BOTTOM"]},
         "Lee Sin":{"id":64,"positions":["JUNGLE"]}}
        """;
    private const string ChampionsVoies = """
        {"data":{
          "Ahri":{"id":"Ahri","key":"103","name":"Ahri","image":{"full":"Ahri.png"}},
          "Jinx":{"id":"Jinx","key":"222","name":"Jinx","image":{"full":"Jinx.png"}},
          "LeeSin":{"id":"LeeSin","key":"64","name":"Lee Sin","image":{"full":"LeeSin.png"}},
          "Teemo":{"id":"Teemo","key":"17","name":"Teemo","image":{"full":"Teemo.png"}},
          "Zyra":{"id":"Zyra","key":"143","name":"Zyra","image":{"full":"Zyra.png"}}}}
        """;

    private static HttpResponseMessage DataDragon(string chemin, Func<HttpResponseMessage> meraki) =>
        chemin == MerakiPath
            ? meraki()
            : Json(chemin.EndsWith("versions.json") ? Versions : ChampionsVoies);

    [Fact]
    public async Task Les_voies_viennent_de_meraki_et_sont_nommees_comme_dans_le_client()
    {
        var catalogue = Catalogue(chemin => DataDragon(chemin, () => Json(Meraki)));

        var champions = (await catalogue.GetAllAsync(Ct)).ToDictionary(c => c.Id);

        Assert.Equal(["mid"], champions["Ahri"].Voies);
        Assert.Equal(["top", "support"], champions["Teemo"].Voies);
        Assert.Equal(["adc"], champions["Jinx"].Voies);
        Assert.Equal(["jungle"], champions["LeeSin"].Voies);
    }

    [Fact]
    public async Task Un_champion_absent_de_meraki_n_a_aucune_voie()
    {
        var catalogue = Catalogue(chemin => DataDragon(chemin, () => Json(Meraki)));

        var zyra = (await catalogue.GetAllAsync(Ct)).Single(c => c.Id == "Zyra");

        Assert.Empty(zyra.Voies);
    }

    [Theory]
    [InlineData("erreur http")]
    [InlineData("json invalide")]
    public async Task Une_panne_de_meraki_laisse_le_catalogue_sans_voies(string panne)
    {
        var catalogue = Catalogue(chemin => DataDragon(chemin, () => panne == "erreur http"
            ? new HttpResponseMessage(HttpStatusCode.BadGateway)
            : Json("{pas du json")));

        var champions = await catalogue.GetAllAsync(Ct);

        Assert.Equal(5, champions.Count);
        Assert.All(champions, c => Assert.Empty(c.Voies));
    }

    private static DataDragonChampionCatalog Catalogue(Func<string, HttpResponseMessage> repondre) =>
        new(new FakeHttpClientFactory(new Repondeur(repondre)), TimeProvider.System);

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    [Fact]
    public async Task Le_catalogue_lit_les_champions_de_la_derniere_version()
    {
        var catalogue = Catalogue(chemin => Json(chemin.EndsWith("versions.json") ? Versions : Champions));

        var champions = await catalogue.GetAllAsync(Ct);

        var ahri = Assert.Single(champions);
        Assert.Empty(ahri.Voies);
        Assert.Equal("https://ddragon.test/cdn/15.1.1/img/champion/Ahri.png", ahri.ImageUrl);
    }

    public static TheoryData<string> Pannes => ["erreur http", "json invalide", "aucune version", "aucun champion"];

    [Theory]
    [MemberData(nameof(Pannes))]
    public async Task Une_panne_de_data_dragon_rend_le_catalogue_indisponible(string panne)
    {
        var catalogue = Catalogue(chemin => panne switch
        {
            "erreur http" => new HttpResponseMessage(HttpStatusCode.InternalServerError),
            "json invalide" => Json("{pas du json"),
            "aucune version" => Json("[]"),
            _ => Json(chemin.EndsWith("versions.json") ? Versions : """{"data":{}}""")
        });

        await Assert.ThrowsAsync<CatalogueIndisponibleException>(() => catalogue.GetAllAsync(Ct));
    }

    private sealed class Repondeur(Func<string, HttpResponseMessage> repondre) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(repondre(request.RequestUri!.AbsolutePath));
    }

    private sealed class FakeHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) =>
            new(handler, disposeHandler: false) { BaseAddress = new Uri("https://ddragon.test/") };
    }
}
