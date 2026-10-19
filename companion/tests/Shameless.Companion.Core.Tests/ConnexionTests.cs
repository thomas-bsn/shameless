using System.Net;
using System.Text;

namespace Shameless.Companion.Core.Tests;

// Les tests passent par ConnexionCompanion (le seam) : une API simulée au niveau HTTP, un stockage en mémoire.
public class ConnexionTests
{
    private const string SerieJson = """
        {"seriesId":12,"format":3,"fearless":false,"teamAWins":1,"teamBWins":0,
         "equipeA":["Alice","Bob"],"equipeB":["Carl","Dora"]}
        """;

    [Fact]
    public async Task Un_Code_accepte_reconnait_la_Serie_et_est_conserve()
    {
        var api = new ApiSimulee().Repond(HttpStatusCode.OK, SerieJson);
        var stockage = new StockageEnMemoire();
        var connexion = new ConnexionCompanion(stockage, api.Client());

        var etat = await connexion.SeConnecterAsync("https://shameless.example/", "CODE-SECRET", TestContext.Current.CancellationToken);

        var serie = Assert.IsType<Connexion.SerieReconnue>(etat.Connexion).Serie;
        Assert.Equal(12, serie.SeriesId);
        Assert.Equal(3, serie.Format);
        Assert.Equal(["Alice", "Bob"], serie.EquipeA);
        Assert.Equal(["Carl", "Dora"], serie.EquipeB);
        Assert.Equal(new IdentifiantsCompanion("https://shameless.example/", "CODE-SECRET"), stockage.Lire());
        Assert.Equal("https://shameless.example/companion/CODE-SECRET", api.DerniereUrl);
    }

    [Fact]
    public async Task Un_Code_refuse_par_l_API_est_efface_et_l_etat_l_indique()
    {
        var api = new ApiSimulee().Repond(HttpStatusCode.NotFound, """{"title":"Code inconnu"}""");
        var stockage = new StockageEnMemoire();
        stockage.Ecrire(new IdentifiantsCompanion("https://shameless.example", "ANCIEN"));
        var connexion = new ConnexionCompanion(stockage, api.Client());

        var etat = await connexion.SeConnecterAsync("https://shameless.example", "ANCIEN", TestContext.Current.CancellationToken);

        Assert.IsType<Connexion.CodeRefuse>(etat.Connexion);
        Assert.Null(stockage.Lire());
    }

    [Fact]
    public async Task Une_API_injoignable_ou_en_erreur_garde_le_Code_stocke()
    {
        var stockage = new StockageEnMemoire();
        var attendu = new IdentifiantsCompanion("https://shameless.example", "CODE");
        stockage.Ecrire(attendu);

        var enErreur = new ConnexionCompanion(stockage, new ApiSimulee().Repond(HttpStatusCode.InternalServerError, "").Client());
        var etatErreur = await enErreur.SeConnecterAsync(attendu.UrlInstance, attendu.Code, TestContext.Current.CancellationToken);

        var coupee = new ConnexionCompanion(stockage, new ApiSimulee().Coupee().Client());
        var etatCoupe = await coupee.SeConnecterAsync(attendu.UrlInstance, attendu.Code, TestContext.Current.CancellationToken);

        Assert.IsType<Connexion.Injoignable>(etatErreur.Connexion);
        Assert.IsType<Connexion.Injoignable>(etatCoupe.Connexion);
        Assert.Equal(attendu, stockage.Lire());
    }

    [Fact]
    public async Task Au_lancement_les_identifiants_stockes_sont_relus_et_la_Serie_reconnue()
    {
        var stockage = new StockageEnMemoire();
        stockage.Ecrire(new IdentifiantsCompanion("https://shameless.example", "CODE"));
        var api = new ApiSimulee().Repond(HttpStatusCode.OK, SerieJson);

        var etat = await new ConnexionCompanion(stockage, api.Client()).DemarrerAsync(TestContext.Current.CancellationToken);

        Assert.IsType<Connexion.SerieReconnue>(etat.Connexion);
        Assert.Equal("https://shameless.example/companion/CODE", api.DerniereUrl);
    }

    [Fact]
    public async Task Au_lancement_sans_identifiants_stockes_rien_n_est_appele()
    {
        var api = new ApiSimulee().Repond(HttpStatusCode.OK, SerieJson);

        var etat = await new ConnexionCompanion(new StockageEnMemoire(), api.Client()).DemarrerAsync(TestContext.Current.CancellationToken);

        Assert.IsType<Connexion.NonConnecte>(etat.Connexion);
        Assert.Null(api.DerniereUrl);
    }

    [Fact]
    public async Task Le_client_LoL_est_signale_detecte_en_valeur_fixe()
    {
        var etat = await new ConnexionCompanion(new StockageEnMemoire(), new ApiSimulee().Client()).DemarrerAsync(TestContext.Current.CancellationToken);

        Assert.True(etat.ClientLolDetecte);
    }

    private sealed class StockageEnMemoire : IStockageIdentifiants
    {
        private IdentifiantsCompanion? _valeur;
        public IdentifiantsCompanion? Lire() => _valeur;
        public void Ecrire(IdentifiantsCompanion identifiants) => _valeur = identifiants;
        public void Effacer() => _valeur = null;
    }

    private sealed class ApiSimulee : HttpMessageHandler
    {
        private HttpStatusCode _statut;
        private string _corps = "";
        public string? DerniereUrl { get; private set; }

        public ApiSimulee Repond(HttpStatusCode statut, string corps)
        {
            _statut = statut;
            _corps = corps;
            return this;
        }

        private bool _coupee;

        public ApiSimulee Coupee()
        {
            _coupee = true;
            return this;
        }

        public HttpClient Client() => new(this);

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            DerniereUrl = request.RequestUri!.ToString();
            if (_coupee) throw new HttpRequestException("connexion refusée");
            return Task.FromResult(new HttpResponseMessage(_statut)
            {
                Content = new StringContent(_corps, Encoding.UTF8, "application/json"),
            });
        }
    }
}
