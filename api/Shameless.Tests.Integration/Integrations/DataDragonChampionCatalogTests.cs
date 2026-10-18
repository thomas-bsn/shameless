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
    private const string Champions = """{"data":{"Ahri":{"id":"Ahri","key":"103","name":"Ahri","tags":["Mage","Assassin"],"image":{"full":"Ahri.png"}}}}""";

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
        Assert.Equal("https://ddragon.test/cdn/15.1.1/img/champion/Ahri.png", ahri.ImageUrl);
        Assert.Equal(["Mage", "Assassin"], ahri.Roles);
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
