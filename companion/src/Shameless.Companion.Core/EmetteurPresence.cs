using System.Net;
using System.Net.Http.Json;

namespace Shameless.Companion.Core;

/// <summary>
/// Signale la présence du Companion à l'API (ADR 0010) : toutes les 30 s, avec le Code Companion et le PUUID
/// du compte connecté au client LoL, tant que le client est détecté ET que la Série est reconnue (un Code
/// stocké : <see cref="ConnexionCompanion"/> l'efface quand l'API le refuse). Sinon, il se tait.
/// </summary>
/// <remarks>
/// Une erreur réseau ou serveur est réessayée au tour suivant, sans bruit : le journal en garde une ligne par
/// changement, la fenêtre rien. Un refus 404 (Série terminée, Code renouvelé) suspend les signaux jusqu'à un
/// autre Code.
/// </remarks>
public sealed class EmetteurPresence(
    IClientLol client,
    IStockageIdentifiants stockage,
    HttpClient http,
    IJournal journal,
    TimeSpan? intervalle = null)
{
    public static readonly TimeSpan IntervalleParDefaut = TimeSpan.FromSeconds(30);

    private readonly TimeSpan _intervalle = intervalle ?? IntervalleParDefaut;
    private readonly SemaphoreSlim _reveil = new(0, 1);
    private volatile bool _clientDetecte;
    private string? _codeRefuse;
    private string? _dernierEchec;

    /// <summary>Le client LoL vient d'être détecté (true) ou perdu (false) ; à relier à <see cref="SurveillanceFinDeGame.ClientDetecteChange"/>.</summary>
    public void ClientDetecte(bool detecte)
    {
        _clientDetecte = detecte;
        if (detecte && _reveil.CurrentCount == 0)
            try { _reveil.Release(); } catch (SemaphoreFullException) { }
    }

    /// <summary>Tourne jusqu'à l'annulation : un tour, puis l'intervalle (ou un réveil à la détection du client).</summary>
    public async Task ExecuterAsync(CancellationToken ct)
    {
        try
        {
            while (!ct.IsCancellationRequested)
            {
                await UnTourAsync(ct);
                await _reveil.WaitAsync(_intervalle, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private async Task UnTourAsync(CancellationToken ct)
    {
        if (!_clientDetecte) return;
        var identifiants = stockage.Lire();
        if (identifiants is null || identifiants.Code == _codeRefuse) return;

        try
        {
            var puuid = await client.LirePuuidAsync(ct);
            if (puuid is null)
            {
                Echec("Présence : aucun compte connecté au client LoL, nouvel essai au prochain tour.");
                return;
            }

            var url = $"{identifiants.UrlInstance.TrimEnd('/')}/companion/{Uri.EscapeDataString(identifiants.Code)}/presence";
            using var reponse = await http.PostAsJsonAsync(url, new { puuid }, ct);
            if (reponse.StatusCode == HttpStatusCode.NotFound)
            {
                _codeRefuse = identifiants.Code;
                journal.Ecrire("Présence : l'API ne reconnaît plus ce Code (Série terminée ou Code renouvelé), signaux suspendus.");
                return;
            }
            reponse.EnsureSuccessStatusCode();
            _dernierEchec = null;
        }
        catch (Exception e) when (e is HttpRequestException or InvalidOperationException or UriFormatException
            || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            Echec($"Présence : signal non envoyé ({e.Message}), nouvel essai au prochain tour.");
        }
    }

    private void Echec(string message)
    {
        if (message == _dernierEchec) return;
        _dernierEchec = message;
        journal.Ecrire(message);
    }
}
