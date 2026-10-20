using Shameless.Companion.Core;

namespace Shameless.Companion.Lcu;

/// <summary>
/// L'API locale du jeu (Live Client Data API) : <c>https://127.0.0.1:2999/liveclientdata/allgamedata</c>, ouverte par le
/// jeu seulement pendant la partie, avec un certificat Riot auto-signé (accepté, comme pour le LCU). Sans authentification.
/// </summary>
public sealed class PartieEnCoursLive : IPartieEnCours, IDisposable
{
    public const string AdresseParDefaut = "https://127.0.0.1:2999";

    private readonly HttpClient _http;

    public PartieEnCoursLive(string adresse = AdresseParDefaut)
    {
        var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
        _http = new HttpClient(handler) { BaseAddress = new Uri(adresse), Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>Le JSON de la partie, ou null : pas de partie (connexion refusée), délai dépassé ou statut d'erreur.</summary>
    public async Task<string?> LireAsync(CancellationToken ct)
    {
        try
        {
            using var reponse = await _http.GetAsync("/liveclientdata/allgamedata", ct);
            return reponse.IsSuccessStatusCode ? await reponse.Content.ReadAsStringAsync(ct) : null;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            return null;
        }
    }

    public void Dispose() => _http.Dispose();
}
