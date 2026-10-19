using Shameless.Companion.Core;

namespace Shameless.Companion.Lcu.Tests;

// Seam : ClientLolLcu comme IClientLol, face à un faux serveur LCU (HTTPS auto-signé + WebSocket WAMP).
public class ClientLolLcuTests : IAsyncLifetime
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), $"lcu-{Guid.NewGuid():N}");
    private readonly CancellationTokenSource _cts = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
    private readonly List<FakeLcuServer> _serveurs = [];

    private string Lockfile => Path.Combine(_dir, "lockfile");

    public ValueTask InitializeAsync()
    {
        _cts.CancelAfter(TimeSpan.FromSeconds(60));
        Directory.CreateDirectory(_dir);
        return ValueTask.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        foreach (var s in _serveurs) await s.DisposeAsync();
        Directory.Delete(_dir, recursive: true);
    }

    private async Task<FakeLcuServer> Serveur(string password = "mot-de-passe-de-test", bool ecrireLockfile = true)
    {
        var serveur = await FakeLcuServer.StartAsync(password);
        _serveurs.Add(serveur);
        if (ecrireLockfile) await serveur.WriteLockfileAsync(Lockfile);
        return serveur;
    }

    private ClientLolLcu Client(Func<IEnumerable<string>>? repertoires = null) =>
        new(new OptionsLcu
        {
            CheminLockfile = repertoires is null ? Lockfile : null,
            RepertoiresClient = repertoires ?? (() => []),
            CheminParDefaut = Path.Combine(_dir, "introuvable", "lockfile"),
            DelaiRedetection = TimeSpan.FromMilliseconds(50),
        });

    private sealed class Flux(IAsyncEnumerator<EvenementClientLol> e, CancellationToken ct)
    {
        public async Task<EvenementClientLol> ProchainAsync()
        {
            var suite = e.MoveNextAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(20), ct);
            Assert.True(await suite, "le flux s'est terminé");
            return e.Current;
        }
    }

    private Flux Suivre(IClientLol client) =>
        new(client.SuivreAsync(_cts.Token).GetAsyncEnumerator(_cts.Token), _cts.Token);

    [Fact]
    public async Task Detecte_le_client_puis_remonte_ses_evenements()
    {
        var lcu = await Serveur();
        var flux = Suivre(Client());

        Assert.IsType<EvenementClientLol.Detecte>(await flux.ProchainAsync());
        await lcu.WaitSubscribedAsync();
        await lcu.PushEventAsync("/lol-end-of-game/v1/eog-stats-block", "Create", """{"gameId":7123456789}""");

        var message = Assert.IsType<EvenementClientLol.Message>(await flux.ProchainAsync());
        Assert.Equal("/lol-end-of-game/v1/eog-stats-block", message.Uri);
        Assert.Equal("Create", message.TypeEvenement);
        Assert.Equal(7123456789, message.Donnees.GetProperty("gameId").GetInt64());
    }

    [Fact]
    public async Task Lit_l_API_locale_avec_l_authentification_Basic_riot()
    {
        var lcu = await Serveur();
        lcu.EogStatsBlockJson = """{"gameId":1,"teams":[]}""";
        var client = Client();
        var flux = Suivre(client);
        await flux.ProchainAsync();

        var reponse = await client.LireAsync("/lol-end-of-game/v1/eog-stats-block", _cts.Token);
        var inconnue = await client.LireAsync("/n/importe/quoi", _cts.Token);

        Assert.Equal(200, reponse.Statut);
        Assert.Equal(lcu.EogStatsBlockJson, reponse.Corps);
        Assert.Equal(404, inconnue.Statut);
    }

    [Fact]
    public async Task Lit_le_puuid_du_compte_connecte_au_client()
    {
        await Serveur();
        IClientLol client = Client();
        var flux = Suivre(client);
        await flux.ProchainAsync();

        Assert.Equal("puuid-du-compte-connecte", await client.LirePuuidAsync(_cts.Token));
    }

    [Fact]
    public async Task Sans_client_detecte_la_lecture_du_puuid_leve_une_erreur_reseau()
    {
        await Assert.ThrowsAsync<HttpRequestException>(() => ((IClientLol)Client()).LirePuuidAsync(_cts.Token));
    }

    [Fact]
    public async Task Un_client_non_connecte_a_un_compte_n_a_pas_de_puuid()
    {
        var lcu = await Serveur();
        lcu.CurrentSummonerJson = """{"errorCode":"RPC_ERROR","httpStatus":404,"message":"not logged in"}""";
        IClientLol client = Client();
        await Suivre(client).ProchainAsync();

        Assert.Null(await client.LirePuuidAsync(_cts.Token));
    }

    [Fact]
    public async Task Un_client_lance_apres_l_app_est_detecte()
    {
        var flux = Suivre(Client());
        var attente = flux.ProchainAsync();
        await Task.Delay(200, _cts.Token);
        Assert.False(attente.IsCompleted);

        await Serveur();

        Assert.IsType<EvenementClientLol.Detecte>(await attente);
    }

    [Fact]
    public async Task Un_lockfile_au_mauvais_mot_de_passe_n_est_pas_un_client_detecte_puis_se_rattrape()
    {
        var lcu = await Serveur(ecrireLockfile: false);
        await File.WriteAllTextAsync(Lockfile, $"LeagueClient:1:{lcu.Port}:faux:https", _cts.Token);
        var flux = Suivre(Client());
        var attente = flux.ProchainAsync();
        await Task.Delay(300, _cts.Token);
        Assert.False(attente.IsCompleted);

        await lcu.WriteLockfileAsync(Lockfile);

        Assert.IsType<EvenementClientLol.Detecte>(await attente);
    }

    [Fact]
    public async Task Redetecte_le_client_apres_un_redemarrage_avec_un_nouveau_port_et_mot_de_passe()
    {
        var premier = await Serveur();
        var client = Client();
        var flux = Suivre(client);
        Assert.IsType<EvenementClientLol.Detecte>(await flux.ProchainAsync());
        await premier.WaitSubscribedAsync();

        await premier.DisposeAsync();
        Assert.IsType<EvenementClientLol.Perdu>(await flux.ProchainAsync());

        var second = await Serveur(password: "autre-mot-de-passe");
        Assert.NotEqual(premier.Port, second.Port);
        Assert.IsType<EvenementClientLol.Detecte>(await flux.ProchainAsync());
        await second.WaitSubscribedAsync();
        await second.PushEventAsync("/lol-gameflow/v1/gameflow-phase", "Update", "\"EndOfGame\"");
        var message = Assert.IsType<EvenementClientLol.Message>(await flux.ProchainAsync());
        Assert.Equal("EndOfGame", message.Donnees.GetString());

        var reponse = await client.LireAsync("/lol-end-of-game/v1/eog-stats-block", _cts.Token);
        Assert.Equal(200, reponse.Statut);
        Assert.Contains(second.Requests, r => r.StartsWith("GET /lol-end-of-game"));
    }

    [Fact]
    public async Task Sans_client_detecte_lire_l_API_echoue_clairement()
    {
        var client = Client();

        await Assert.ThrowsAsync<HttpRequestException>(() => client.LireAsync("/x", _cts.Token));
    }

    [Fact]
    public async Task Prend_le_lockfile_du_dossier_du_client_en_cours_d_execution()
    {
        var installation = Path.Combine(_dir, "League of Legends");
        Directory.CreateDirectory(installation);
        var lcu = await Serveur(ecrireLockfile: false);
        await lcu.WriteLockfileAsync(Path.Combine(installation, "lockfile"));

        var flux = Suivre(Client(() => [installation]));

        Assert.IsType<EvenementClientLol.Detecte>(await flux.ProchainAsync());
    }
}
