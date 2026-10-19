using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Shameless.Companion.Core;

namespace Shameless.Companion.Lcu;

public sealed class OptionsLcu
{
    /// <summary>Chemin explicite du lockfile ; null : le dossier du client LoL en cours d'exécution, puis <see cref="CheminParDefaut"/>.</summary>
    public string? CheminLockfile { get; init; }

    public Func<IEnumerable<string>> RepertoiresClient { get; init; } = Lockfile.RunningClientDirectories;
    public string CheminParDefaut { get; init; } = Lockfile.DefaultPath;

    /// <summary>Délai entre deux tentatives quand le client est absent, injoignable ou fermé.</summary>
    public TimeSpan DelaiRedetection { get; init; } = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Le module LCU (ADR 0004) : lit le lockfile, s'authentifie (Basic riot:mot de passe, certificat auto-signé accepté),
/// s'abonne à <c>OnJsonApiEvent</c> sur le WebSocket WAMP, et redétecte le client quand il redémarre.
/// Tout le reste du Companion ne le connaît que par <see cref="IClientLol"/>.
/// </summary>
public sealed class ClientLolLcu(OptionsLcu? options = null, IJournal? journal = null) : IClientLol
{
    private readonly OptionsLcu _options = options ?? new OptionsLcu();
    private volatile HttpClient? _http;

    public async Task<ReponseLcu> LireAsync(string chemin, CancellationToken ct)
    {
        var http = _http ?? throw new HttpRequestException("Client LoL non détecté.");
        using var reponse = await http.GetAsync(chemin, ct);
        return new ReponseLcu((int)reponse.StatusCode, await reponse.Content.ReadAsStringAsync(ct));
    }

    public async IAsyncEnumerable<EvenementClientLol> SuivreAsync([EnumeratorCancellation] CancellationToken ct)
    {
        string? dernierEchec = null;
        while (!ct.IsCancellationRequested)
        {
            var (connexion, echec) = await ConnecterAsync(ct);
            if (connexion is null)
            {
                if (echec != dernierEchec) journal?.Ecrire(echec!);
                dernierEchec = echec;
                await Task.Delay(_options.DelaiRedetection, ct);
                continue;
            }
            dernierEchec = null;

            using (connexion)
            {
                _http = connexion.Http;
                yield return new EvenementClientLol.Detecte();

                while (await connexion.RecevoirAsync(ct) is { } message)
                    if (LireEvenement(message) is { } evenement)
                        yield return evenement;

                _http = null;
            }
            yield return new EvenementClientLol.Perdu();
            await Task.Delay(_options.DelaiRedetection, ct);
        }
    }

    /// <summary>Lit le lockfile, ouvre le WebSocket et s'abonne. Échec : null et la raison (client absent, mot de passe refusé…).</summary>
    private async Task<(Connexion? Connexion, string? Echec)> ConnecterAsync(CancellationToken ct)
    {
        Lockfile lockfile;
        try
        {
            var chemin = Lockfile.Locate(_options.CheminLockfile, _options.RepertoiresClient(), _options.CheminParDefaut);
            lockfile = Lockfile.Read(chemin);
        }
        catch (Exception e) when (e is FileNotFoundException or DirectoryNotFoundException or FormatException or IOException or UnauthorizedAccessException)
        {
            return (null, $"Client LoL non détecté : {e.Message}");
        }

        var authentification = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"riot:{lockfile.Password}"));
        var handler = new HttpClientHandler { ServerCertificateCustomValidationCallback = (_, _, _, _) => true };
        var http = new HttpClient(handler) { BaseAddress = new Uri($"https://127.0.0.1:{lockfile.Port}") };
        http.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authentification);
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", authentification);
        socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        try
        {
            await socket.ConnectAsync(new Uri($"wss://127.0.0.1:{lockfile.Port}/"), ct);
            await socket.SendAsync(Encoding.UTF8.GetBytes("[5,\"OnJsonApiEvent\"]"), WebSocketMessageType.Text, true, ct);
            return (new Connexion(http, socket), null);
        }
        catch (Exception e) when (e is WebSocketException or HttpRequestException or IOException)
        {
            socket.Dispose();
            http.Dispose();
            return (null, $"Client LoL injoignable sur le port {lockfile.Port} (lockfile périmé ou mot de passe refusé) : {e.Message}");
        }
    }

    /// <summary>Événement WAMP : <c>[8, "OnJsonApiEvent", {uri, eventType, data}]</c>. Le reste est ignoré.</summary>
    private static EvenementClientLol.Message? LireEvenement(string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var racine = doc.RootElement;
            if (racine.ValueKind != JsonValueKind.Array || racine.GetArrayLength() != 3
                || racine[0].GetInt32() != 8 || racine[2].ValueKind != JsonValueKind.Object)
                return null;

            var charge = racine[2];
            var uri = charge.TryGetProperty("uri", out var u) ? u.GetString() : null;
            if (uri is null) return null;
            var type = charge.TryGetProperty("eventType", out var t) ? t.GetString() ?? "" : "";
            var donnees = charge.TryGetProperty("data", out var d) ? d.Clone() : default;
            return new EvenementClientLol.Message(uri, type, donnees);
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or FormatException)
        {
            return null;
        }
    }

    private sealed class Connexion(HttpClient http, ClientWebSocket socket) : IDisposable
    {
        private readonly byte[] _tampon = new byte[64 * 1024];

        public HttpClient Http { get; } = http;

        /// <summary>Le prochain message texte ; null quand le client ferme ou coupe la connexion.</summary>
        public async Task<string?> RecevoirAsync(CancellationToken ct)
        {
            try
            {
                while (socket.State == WebSocketState.Open)
                {
                    using var flux = new MemoryStream();
                    WebSocketReceiveResult resultat;
                    do
                    {
                        resultat = await socket.ReceiveAsync(_tampon, ct);
                        if (resultat.MessageType == WebSocketMessageType.Close) return null;
                        flux.Write(_tampon, 0, resultat.Count);
                    } while (!resultat.EndOfMessage);

                    // L'ack d'abonnement arrive parfois sans corps.
                    if (flux.Length > 0) return Encoding.UTF8.GetString(flux.ToArray());
                }
                return null;
            }
            catch (Exception e) when (e is WebSocketException or IOException)
            {
                return null;
            }
        }

        public void Dispose()
        {
            socket.Dispose();
            Http.Dispose();
        }
    }
}
