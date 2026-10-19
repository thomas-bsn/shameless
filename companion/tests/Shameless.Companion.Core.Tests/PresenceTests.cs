using System.Net;
using System.Text;
using System.Text.Json;

namespace Shameless.Companion.Core.Tests;

// Seam : EmetteurPresence, avec un IClientLol simulé, une API simulée au niveau HTTP et un stockage en mémoire.
public class PresenceTests : IAsyncLifetime
{
    private const string UriSummoner = "/lol-summoner/v1/current-summoner";
    private static readonly IdentifiantsCompanion Identifiants = new("https://shameless.example/", "CODE-SECRET");
    private static readonly CancellationToken Ct = TestContext.Current.CancellationToken;

    private readonly ClientLolSimule _client = new();
    private readonly ApiDePresence _api = new();
    private readonly StockageDePresence _stockage = new();
    private readonly JournalEnMemoire _journal = new();
    private readonly CancellationTokenSource _arret = CancellationTokenSource.CreateLinkedTokenSource(Ct);
    private Task? _boucle;

    public ValueTask InitializeAsync()
    {
        _client.Reponses[UriSummoner] = [new(200, """{"puuid":"puuid-sam"}""")];
        _arret.CancelAfter(TimeSpan.FromSeconds(30));
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _arret.CancelAsync();
        if (_boucle is not null) await _boucle;
    }

    private EmetteurPresence Emetteur()
    {
        var emetteur = new EmetteurPresence(_client, _stockage, _api.Client(), _journal, TimeSpan.FromMilliseconds(40));
        _boucle = emetteur.ExecuterAsync(_arret.Token);
        return emetteur;
    }

    private async Task AttendreAsync(Func<bool> condition)
    {
        var limite = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < limite, "condition jamais atteinte");
            await Task.Delay(10, Ct);
        }
    }

    [Fact]
    public async Task Envoie_le_signal_avec_le_Code_et_le_puuid_tant_que_le_client_est_detecte_et_la_Serie_reconnue()
    {
        _stockage.Ecrire(Identifiants);
        var emetteur = Emetteur();

        emetteur.ClientDetecte(true);

        await AttendreAsync(() => _api.Signaux.Count >= 3);
        var signal = _api.Signaux.First();
        Assert.Equal("https://shameless.example/companion/CODE-SECRET/presence", signal.Url);
        Assert.Equal("puuid-sam", JsonDocument.Parse(signal.Corps).RootElement.GetProperty("puuid").GetString());
    }

    [Fact]
    public async Task N_envoie_rien_sans_client_detecte()
    {
        _stockage.Ecrire(Identifiants);
        Emetteur();

        await Task.Delay(300, Ct);

        Assert.Empty(_api.Signaux);
    }

    [Fact]
    public async Task N_envoie_rien_sans_Serie_reconnue()
    {
        var emetteur = Emetteur();

        emetteur.ClientDetecte(true);
        await Task.Delay(300, Ct);

        Assert.Empty(_api.Signaux);
    }

    [Fact]
    public async Task S_arrete_quand_le_client_est_perdu_et_reprend_quand_il_revient()
    {
        _stockage.Ecrire(Identifiants);
        var emetteur = Emetteur();
        emetteur.ClientDetecte(true);
        await AttendreAsync(() => _api.Signaux.Count >= 1);

        emetteur.ClientDetecte(false);
        await Task.Delay(100, Ct);
        var apresPerte = _api.Signaux.Count;
        await Task.Delay(300, Ct);
        Assert.Equal(apresPerte, _api.Signaux.Count);

        emetteur.ClientDetecte(true);
        await AttendreAsync(() => _api.Signaux.Count > apresPerte);
    }

    [Fact]
    public async Task S_arrete_quand_la_Serie_n_est_plus_reconnue()
    {
        _stockage.Ecrire(Identifiants);
        var emetteur = Emetteur();
        emetteur.ClientDetecte(true);
        await AttendreAsync(() => _api.Signaux.Count >= 1);

        _stockage.Effacer();
        await Task.Delay(100, Ct);
        var apresEffacement = _api.Signaux.Count;
        await Task.Delay(300, Ct);

        Assert.Equal(apresEffacement, _api.Signaux.Count);
    }

    [Fact]
    public async Task Un_refus_de_l_API_Serie_terminee_ou_Code_renouvele_arrete_les_signaux_jusqu_a_un_nouveau_Code()
    {
        _stockage.Ecrire(Identifiants);
        _api.Statut = HttpStatusCode.NotFound;
        var emetteur = Emetteur();
        emetteur.ClientDetecte(true);
        await AttendreAsync(() => _api.Signaux.Count >= 1);

        await Task.Delay(300, Ct);
        Assert.Single(_api.Signaux);

        _api.Statut = HttpStatusCode.NoContent;
        _stockage.Ecrire(Identifiants with { Code = "NOUVEAU-CODE" });
        await AttendreAsync(() => _api.Signaux.Any(s => s.Url.EndsWith("/companion/NOUVEAU-CODE/presence")));
    }

    [Fact]
    public async Task Une_erreur_reseau_ou_serveur_est_retentee_au_tour_suivant_sans_arreter_l_emetteur()
    {
        _stockage.Ecrire(Identifiants);
        _api.Coupee = true;
        var emetteur = Emetteur();
        emetteur.ClientDetecte(true);
        await AttendreAsync(() => _api.Tentatives >= 2);

        _api.Coupee = false;
        _api.Statut = HttpStatusCode.InternalServerError;
        var tentatives = _api.Tentatives;
        await AttendreAsync(() => _api.Tentatives >= tentatives + 2);

        _api.Statut = HttpStatusCode.NoContent;
        var signaux = _api.Signaux.Count;
        await AttendreAsync(() => _api.Signaux.Count > signaux && _api.DernierStatutRepondu == HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Un_client_sans_compte_connecte_ou_en_erreur_n_envoie_rien_et_reessaie()
    {
        _stockage.Ecrire(Identifiants);
        _client.Reponses[UriSummoner] = [new(404, "{}"), new(404, "{}")];
        var emetteur = Emetteur();
        emetteur.ClientDetecte(true);
        await AttendreAsync(() => _client.Lectures.Count(l => l == UriSummoner) >= 2);
        Assert.Empty(_api.Signaux);

        _client.Reponses[UriSummoner] = [new(200, """{"puuid":"puuid-sam"}""")];

        await AttendreAsync(() => _api.Signaux.Count >= 1);
    }

    private sealed class StockageDePresence : IStockageIdentifiants
    {
        private IdentifiantsCompanion? _valeur;
        public IdentifiantsCompanion? Lire() => Volatile.Read(ref _valeur);
        public void Ecrire(IdentifiantsCompanion identifiants) => Volatile.Write(ref _valeur, identifiants);
        public void Effacer() => Volatile.Write(ref _valeur, null);
    }

    private sealed class ApiDePresence : HttpMessageHandler
    {
        private int _tentatives;
        public HttpStatusCode Statut { get; set; } = HttpStatusCode.NoContent;
        public bool Coupee { get; set; }
        public HttpStatusCode DernierStatutRepondu { get; private set; }
        public int Tentatives => Volatile.Read(ref _tentatives);
        public System.Collections.Concurrent.ConcurrentQueue<(string Url, string Corps)> SignauxRecus { get; } = new();

        /// <summary>Les signaux que l'API a reçus (qu'elle les ait acceptés ou refusés).</summary>
        public IReadOnlyCollection<(string Url, string Corps)> Signaux => SignauxRecus;

        public HttpClient Client() => new(this);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Interlocked.Increment(ref _tentatives);
            if (Coupee) throw new HttpRequestException("connexion refusée");
            SignauxRecus.Enqueue((request.RequestUri!.ToString(), await request.Content!.ReadAsStringAsync(ct)));
            DernierStatutRepondu = Statut;
            return new HttpResponseMessage(Statut) { Content = new StringContent("", Encoding.UTF8, "application/json") };
        }
    }
}
