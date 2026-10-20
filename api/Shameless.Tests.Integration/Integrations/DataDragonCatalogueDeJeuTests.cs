using System.Net;
using System.Text;
using Shameless.Application.Interfaces;
using Shameless.Infrastructure.Integrations.DataDragon;

namespace Shameless.Tests.Integration.Integrations;

/// <summary>Le catalogue des objets, sorts et runes face à un Data Dragon simulé : noms français, URL d'icônes, pannes.</summary>
public class DataDragonCatalogueDeJeuTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private const string Items = """{"data":{"6655":{"name":"Rayon de Luden","image":{"full":"6655.png"}},"1001":{"name":"Bottes","image":{"full":"1001.png"}}}}""";
    private const string Sorts = """{"data":{"SummonerFlash":{"key":"4","name":"Saut éclair","image":{"full":"SummonerFlash.png"}}}}""";
    private const string Runes = """
        [{"id":8100,"key":"Domination","icon":"perk-images/Styles/7200_Domination.png","name":"Domination",
          "slots":[{"runes":[{"id":8112,"key":"Electrocute","icon":"perk-images/Styles/Domination/Electrocute/Electrocute.png","name":"Électrocution"}]},
                   {"runes":[{"id":8126,"key":"CheapShot","icon":"perk-images/Styles/Domination/CheapShot/CheapShot.png","name":"Coup bas"}]}]}]
        """;

    private static HttpResponseMessage Reponse(string chemin) => chemin switch
    {
        var c when c.EndsWith("versions.json") => Json("""["15.1.1","15.0.1"]"""),
        var c when c.EndsWith("/fr_FR/item.json") => Json(Items),
        var c when c.EndsWith("/fr_FR/summoner.json") => Json(Sorts),
        var c when c.EndsWith("/fr_FR/runesReforged.json") => Json(Runes),
        _ => new HttpResponseMessage(HttpStatusCode.NotFound)
    };

    [Fact]
    public async Task Le_catalogue_lit_objets_sorts_et_runes_de_la_derniere_version_en_francais_avec_leurs_icones()
    {
        var catalogue = await Catalogue(Reponse).GetAsync(Ct);

        Assert.Equal("15.1.1", catalogue.Version);
        var luden = catalogue.Objets.Single(o => o.Id == 6655);
        Assert.Equal("Rayon de Luden", luden.Name);
        Assert.Equal("https://ddragon.test/cdn/15.1.1/img/item/6655.png", luden.IconUrl);
        var flash = Assert.Single(catalogue.Sorts);
        Assert.Equal((4, "Saut éclair", "https://ddragon.test/cdn/15.1.1/img/spell/SummonerFlash.png"), (flash.Id, flash.Name, flash.IconUrl));
        var domination = Assert.Single(catalogue.Arbres);
        Assert.Equal("https://ddragon.test/cdn/img/perk-images/Styles/7200_Domination.png", domination.IconUrl);
        Assert.Equal([8112, 8126], domination.Runes.Select(r => r.Id));
        Assert.Equal("https://ddragon.test/cdn/img/perk-images/Styles/Domination/Electrocute/Electrocute.png", domination.Runes[0].IconUrl);
    }

    [Theory]
    [InlineData("item.json")]
    [InlineData("summoner.json")]
    [InlineData("runesReforged.json")]
    public async Task Une_panne_sur_un_des_fichiers_rend_le_catalogue_indisponible(string fichier)
    {
        var catalogue = Catalogue(chemin => chemin.EndsWith(fichier) ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Reponse(chemin));

        await Assert.ThrowsAsync<CatalogueIndisponibleException>(() => catalogue.GetAsync(Ct));
    }

    [Fact]
    public async Task Une_panne_au_rafraichissement_sert_le_catalogue_perime_et_attend_avant_de_reessayer()
    {
        var horloge = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var panne = false;
        var appels = 0;
        var catalogue = new DataDragonCatalogueDeJeu(new FakeHttpClientFactory(new Repondeur(chemin =>
        {
            appels++;
            return panne ? new HttpResponseMessage(HttpStatusCode.InternalServerError) : Reponse(chemin);
        })), horloge);
        var premier = await catalogue.GetAsync(Ct);
        var appelsDuChargement = appels;

        horloge.Advance(TimeSpan.FromHours(7));
        panne = true;
        var enPanne = await catalogue.GetAsync(Ct);
        var appelsApresLaPremierePanne = appels;
        var encore = await catalogue.GetAsync(Ct);

        Assert.Same(premier, enPanne);
        Assert.Same(premier, encore);
        Assert.True(appelsApresLaPremierePanne > appelsDuChargement); // une tentative a bien eu lieu
        Assert.Equal(appelsApresLaPremierePanne, appels);             // aucune autre avant le délai

        horloge.Advance(TimeSpan.FromMinutes(10));
        panne = false;
        var rafraichi = await catalogue.GetAsync(Ct);

        Assert.NotSame(premier, rafraichi);
    }

    [Fact]
    public async Task Sans_aucun_catalogue_en_memoire_une_panne_reste_indisponible_sans_marteler_Data_Dragon()
    {
        var horloge = new Microsoft.Extensions.Time.Testing.FakeTimeProvider();
        var appels = 0;
        var catalogue = new DataDragonCatalogueDeJeu(new FakeHttpClientFactory(new Repondeur(_ =>
        {
            appels++;
            return new HttpResponseMessage(HttpStatusCode.InternalServerError);
        })), horloge);

        await Assert.ThrowsAsync<CatalogueIndisponibleException>(() => catalogue.GetAsync(Ct));
        var apresLaPremiere = appels;
        await Assert.ThrowsAsync<CatalogueIndisponibleException>(() => catalogue.GetAsync(Ct));
        Assert.Equal(apresLaPremiere, appels);

        horloge.Advance(TimeSpan.FromMinutes(10));
        await Assert.ThrowsAsync<CatalogueIndisponibleException>(() => catalogue.GetAsync(Ct));
        Assert.True(appels > apresLaPremiere);
    }

    private static DataDragonCatalogueDeJeu Catalogue(Func<string, HttpResponseMessage> repondre) =>
        new(new FakeHttpClientFactory(new Repondeur(repondre)), TimeProvider.System);

    private static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

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
