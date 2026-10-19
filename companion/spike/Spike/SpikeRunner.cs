using System.Net;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace Spike;

public sealed class SpikeOptions
{
    public string LockfilePath { get; set; } = Lockfile.DefaultPath;
    public string OutputRoot { get; set; } = "captures";
    public int RetryCount { get; set; } = 10;
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(3);

    /// <summary>S'arrête après n captures (0 : tourne jusqu'à Ctrl+C).</summary>
    public int StopAfterCaptures { get; set; }

    /// <summary>Enregistre aussi les événements sans rapport avec la fin de game.</summary>
    public bool RawAllEvents { get; set; }

    public Action<string> Log { get; set; } = line => Console.WriteLine($"{DateTime.Now:HH:mm:ss.fff} {line}");
}

/// <summary>
/// Se branche sur l'API locale du client LoL (lockfile, HTTPS, WebSocket WAMP) et enregistre,
/// à chaque fin de game, le bloc de fin de game et la game de l'historique local.
/// </summary>
public static class SpikeRunner
{
    private const string EogUri = "/lol-end-of-game/v1/eog-stats-block";
    private const string GameflowUri = "/lol-gameflow/v1/gameflow-phase";

    private static readonly string[] RelevantUriParts = ["end-of-game", "gameflow", "match-history", "postgame"];

    public static async Task<IReadOnlyList<string>> RunAsync(SpikeOptions options, CancellationToken ct)
    {
        var log = options.Log;
        var lockfile = Lockfile.Read(options.LockfilePath);
        log($"Lockfile lu : port {lockfile.Port}, protocole {lockfile.Protocol}");

        using var http = CreateHttpClient(lockfile);
        await ProbeAsync(http, log, ct);

        Directory.CreateDirectory(options.OutputRoot);
        var eventsPath = Path.Combine(options.OutputRoot, $"events-{DateTime.Now:yyyyMMdd-HHmmss}.jsonl");
        log($"Événements bruts enregistrés dans {eventsPath}");

        using var socket = new ClientWebSocket();
        socket.Options.SetRequestHeader("Authorization", BasicAuth(lockfile));
        socket.Options.RemoteCertificateValidationCallback = (_, _, _, _) => true;
        try
        {
            await socket.ConnectAsync(new Uri($"wss://127.0.0.1:{lockfile.Port}/"), ct);
        }
        catch (WebSocketException e)
        {
            throw new InvalidOperationException($"Connexion WebSocket refusée : {e.Message}", e);
        }
        log("WebSocket connectée");
        await SendAsync(socket, "[5,\"OnJsonApiEvent\"]", ct);
        log("Abonné à OnJsonApiEvent. En attente de la fin d'une game (Ctrl+C pour arrêter).");

        var captured = new List<string>();
        var claimedGames = new HashSet<long>();
        var tasks = new List<Task>();
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await foreach (var message in ReceiveAsync(socket, stop.Token))
            {
                var evt = LcuEvent.TryParse(message);
                if (evt is null)
                {
                    log($"Message WebSocket non reconnu : {Truncate(message, 200)}");
                    continue;
                }

                log($"Événement {evt.EventType} {evt.Uri}");

                var relevant = options.RawAllEvents || RelevantUriParts.Any(p => evt.Uri.Contains(p, StringComparison.Ordinal));
                if (relevant)
                    await File.AppendAllTextAsync(eventsPath, evt.RawJson + "\n", stop.Token);

                if (!IsEndOfGameTrigger(evt)) continue;

                tasks.Add(Task.Run(async () =>
                {
                    var dir = await CaptureAsync(http, options, evt, claimedGames, stop.Token);
                    if (dir is null) return;
                    lock (captured) captured.Add(dir);
                    if (options.StopAfterCaptures > 0 && captured.Count >= options.StopAfterCaptures)
                        stop.Cancel();
                }, stop.Token));
            }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested)
        {
        }

        if (!ct.IsCancellationRequested || captured.Count > 0)
            await Task.WhenAll(tasks.Select(t => t.ContinueWith(_ => { })));
        lock (captured) return captured.ToList();
    }

    private static bool IsEndOfGameTrigger(LcuEvent evt)
    {
        if (evt.EventType == "Delete") return false;
        if (evt.Uri == EogUri) return true;
        return evt.Uri == GameflowUri && evt.Data.ValueKind == JsonValueKind.String
               && evt.Data.GetString() is "EndOfGame" or "PreEndOfGame";
    }

    private static async Task<string?> CaptureAsync(
        HttpClient http, SpikeOptions options, LcuEvent trigger, HashSet<long> claimedGames, CancellationToken ct)
    {
        var log = options.Log;
        log($"Fin de game détectée ({trigger.Uri}) : capture du bloc de fin de game");

        var eog = await GetWithRetriesAsync(http, EogUri, options, ct, "eog-stats-block");
        if (eog is null)
        {
            log("Abandon : le bloc de fin de game n'a jamais répondu 200.");
            return null;
        }

        var gameId = ReadGameId(eog) ?? ReadGameId(trigger.Data);
        if (gameId is null)
        {
            log("Abandon : pas de gameId lisible dans le bloc de fin de game (voir eog-stats-block.json).");
            gameId = 0;
        }

        lock (claimedGames)
            if (gameId != 0 && !claimedGames.Add(gameId.Value))
            {
                log($"Game {gameId} déjà capturée, déclencheur ignoré.");
                return null;
            }

        var dir = Path.Combine(options.OutputRoot, $"{DateTime.Now:yyyyMMdd-HHmmss}-game-{gameId}");
        Directory.CreateDirectory(dir);
        await File.WriteAllTextAsync(Path.Combine(dir, "eog-stats-block.json"), eog, ct);
        log($"Écrit {Path.Combine(dir, "eog-stats-block.json")}");

        var history = await GetWithRetriesAsync(http, $"/lol-match-history/v1/games/{gameId}", options, ct, "historique");
        if (history is null)
            log($"Abandon de l'historique après {options.RetryCount} essais : seul le bloc de fin de game est enregistré.");
        else
        {
            await File.WriteAllTextAsync(Path.Combine(dir, "match-history-game.json"), history, ct);
            log($"Écrit {Path.Combine(dir, "match-history-game.json")}");
        }

        return dir;
    }

    private static async Task<string?> GetWithRetriesAsync(
        HttpClient http, string path, SpikeOptions options, CancellationToken ct, string label)
    {
        for (var attempt = 1; attempt <= options.RetryCount; attempt++)
        {
            try
            {
                using var response = await http.GetAsync(path, ct);
                var body = await response.Content.ReadAsStringAsync(ct);
                options.Log($"GET {path} -> {(int)response.StatusCode} ({label}, essai {attempt}/{options.RetryCount}, {body.Length} octets)");
                if (response.StatusCode == HttpStatusCode.OK && body.Length > 2) return body;
                if (response.StatusCode != HttpStatusCode.OK)
                    options.Log($"  réponse : {Truncate(body, 300)}");
            }
            catch (HttpRequestException e)
            {
                options.Log($"GET {path} : erreur réseau ({e.Message}), essai {attempt}/{options.RetryCount}");
            }

            if (attempt < options.RetryCount) await Task.Delay(options.RetryDelay, ct);
        }
        return null;
    }

    private static long? ReadGameId(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return ReadGameId(doc.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long? ReadGameId(JsonElement element)
        => element.ValueKind == JsonValueKind.Object
           && element.TryGetProperty("gameId", out var id)
           && id.ValueKind == JsonValueKind.Number
           && id.TryGetInt64(out var value)
            ? value
            : null;

    private static async Task ProbeAsync(HttpClient http, Action<string> log, CancellationToken ct)
    {
        const string path = "/lol-gameflow/v1/gameflow-phase";
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(path, ct);
        }
        catch (HttpRequestException e)
        {
            throw new InvalidOperationException(
                $"Client LoL injoignable ({e.Message}). Lockfile périmé ? Relance le client.", e);
        }
        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            log($"GET {path} -> {(int)response.StatusCode} {Truncate(body, 100)}");
            if (response.StatusCode == HttpStatusCode.Unauthorized)
                throw new InvalidOperationException("Réponse 401 : mot de passe du lockfile refusé (lockfile périmé ? relance le client).");
        }
    }

    private static HttpClient CreateHttpClient(Lockfile lockfile)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = (_, _, _, _) => true,
        };
        var http = new HttpClient(handler) { BaseAddress = new Uri($"https://127.0.0.1:{lockfile.Port}") };
        http.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(BasicAuth(lockfile));
        http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return http;
    }

    private static string BasicAuth(Lockfile lockfile)
        => "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"riot:{lockfile.Password}"));

    private static Task SendAsync(ClientWebSocket socket, string text, CancellationToken ct)
        => socket.SendAsync(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text, true, ct);

    private static async IAsyncEnumerable<string> ReceiveAsync(
        ClientWebSocket socket, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        var buffer = new byte[64 * 1024];
        while (socket.State == WebSocketState.Open)
        {
            using var ms = new MemoryStream();
            WebSocketReceiveResult result;
            do
            {
                result = await socket.ReceiveAsync(buffer, ct);
                if (result.MessageType == WebSocketMessageType.Close) yield break;
                ms.Write(buffer, 0, result.Count);
            } while (!result.EndOfMessage);

            // Les réponses vides (ack d'abonnement) arrivent parfois sans corps.
            if (ms.Length > 0) yield return Encoding.UTF8.GetString(ms.ToArray());
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}

/// <summary>Événement WAMP : <c>[8, "OnJsonApiEvent", {uri, eventType, data}]</c>.</summary>
public sealed record LcuEvent(string Uri, string EventType, JsonElement Data, string RawJson)
{
    public static LcuEvent? TryParse(string message)
    {
        try
        {
            using var doc = JsonDocument.Parse(message);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() != 3
                || root[0].GetInt32() != 8 || root[2].ValueKind != JsonValueKind.Object)
                return null;

            var payload = root[2];
            var uri = payload.TryGetProperty("uri", out var u) ? u.GetString() : null;
            if (uri is null) return null;
            var type = payload.TryGetProperty("eventType", out var t) ? t.GetString() ?? "" : "";
            var data = payload.TryGetProperty("data", out var d) ? d.Clone() : default;
            return new LcuEvent(uri, type, data, payload.GetRawText());
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}
