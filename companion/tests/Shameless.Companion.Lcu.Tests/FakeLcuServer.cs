using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Shameless.Companion.Lcu.Tests;

/// <summary>
/// Faux client LoL : HTTPS auto-signé + WebSocket WAMP, avec authentification Basic riot:motdepasse.
/// Rejoue des JSON d'exemple ; ne reproduit pas le format réel.
/// </summary>
public sealed class FakeLcuServer : IAsyncDisposable
{
    public string Password { get; private init; } = "mot-de-passe-de-test";

    private readonly WebApplication _app;
    private readonly TaskCompletionSource _subscribed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConcurrentQueue<WebSocket> _sockets = new();
    private int _matchHistoryCalls;

    public int Port { get; private set; }
    public string EogStatsBlockJson { get; set; } = """{"gameId":7123456789,"teams":[]}""";
    public string CurrentSummonerJson { get; set; } = """{"puuid":"puuid-du-compte-connecte","gameName":"Sam","tagLine":"EUW"}""";
    public string MatchHistoryJson { get; set; } = """{"gameId":7123456789,"platformId":"EUW1","participants":[]}""";

    /// <summary>Nombre de réponses 404 de l'historique avant la première 200.</summary>
    public int MatchHistoryNotReadyCount { get; set; }

    public ConcurrentQueue<string> Requests { get; } = new();

    private FakeLcuServer(WebApplication app) => _app = app;

    public static async Task<FakeLcuServer> StartAsync(string password = "mot-de-passe-de-test")
    {
        var cert = CreateSelfSignedCertificate();
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0, o =>
        {
            o.UseHttps(cert);
            o.Protocols = HttpProtocols.Http1;
        }));

        var app = builder.Build();
        var server = new FakeLcuServer(app) { Password = password };
        server.Configure(app);
        await app.StartAsync();
        server.Port = new Uri(app.Urls.First()).Port;
        return server;
    }

    public async Task WriteLockfileAsync(string path)
        => await File.WriteAllTextAsync(path, $"LeagueClient:1234:{Port}:{Password}:https");

    public Task WaitSubscribedAsync() => _subscribed.Task.WaitAsync(TimeSpan.FromSeconds(10));

    public async Task PushEventAsync(string uri, string eventType, string dataJson)
    {
        var message = $$"""[8,"OnJsonApiEvent",{"uri":"{{uri}}","eventType":"{{eventType}}","data":{{dataJson}}}]""";
        var bytes = Encoding.UTF8.GetBytes(message);
        foreach (var socket in _sockets.Where(s => s.State == WebSocketState.Open))
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
    }

    private void Configure(WebApplication app)
    {
        app.UseWebSockets();
        app.Use(async (ctx, next) =>
        {
            Requests.Enqueue($"{ctx.Request.Method} {ctx.Request.Path}");
            var expected = "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"riot:{Password}"));
            if (ctx.Request.Headers.Authorization != expected)
            {
                ctx.Response.StatusCode = 401;
                return;
            }
            await next();
        });

        app.Map("/", async ctx =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest)
            {
                ctx.Response.StatusCode = 400;
                return;
            }
            using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
            _sockets.Enqueue(socket);
            var buffer = new byte[4096];
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, ctx.RequestAborted);
                if (result.MessageType == WebSocketMessageType.Close) break;
                var text = Encoding.UTF8.GetString(buffer, 0, result.Count);
                if (text.Contains("OnJsonApiEvent")) _subscribed.TrySetResult();
            }
        });

        app.MapGet("/lol-end-of-game/v1/eog-stats-block",
            () => Results.Content(EogStatsBlockJson, "application/json"));

        app.MapGet("/lol-summoner/v1/current-summoner",
            () => Results.Content(CurrentSummonerJson, "application/json"));

        app.MapGet("/lol-match-history/v1/games/{gameId}", (long gameId) =>
        {
            if (Interlocked.Increment(ref _matchHistoryCalls) <= MatchHistoryNotReadyCount)
                return Results.Content("""{"errorCode":"RPC_ERROR","httpStatus":404,"message":"not ready"}""",
                    "application/json", statusCode: 404);
            return Results.Content(MatchHistoryJson, "application/json");
        });
    }

    private static X509Certificate2 CreateSelfSignedCertificate()
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest("CN=127.0.0.1", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var cert = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(1));
        return X509CertificateLoader.LoadPkcs12(cert.Export(X509ContentType.Pfx), null);
    }

    /// <summary>Simule la fermeture du client : les WebSocket se ferment, le serveur s'arrête.</summary>
    public async ValueTask DisposeAsync()
    {
        foreach (var socket in _sockets.Where(s => s.State == WebSocketState.Open))
        {
            try { await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None); }
            catch (WebSocketException) { }
        }
        // Pas d'arrêt gracieux de 30 s : les WebSocket du faux client sont coupées.
        using var delai = new CancellationTokenSource(TimeSpan.FromSeconds(1));
        try { await _app.StopAsync(delai.Token); } catch (OperationCanceledException) { }
        await _app.DisposeAsync();
    }
}
