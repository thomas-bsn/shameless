using Spike;

namespace Spike.Tests;

public class CaptureTests : IAsyncLifetime
{
    private FakeLcuServer _lcu = null!;
    private string _dir = null!;
    private readonly List<string> _log = new();

    public async Task InitializeAsync()
    {
        _dir = Path.Combine(Path.GetTempPath(), $"spike-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_dir);
        _lcu = await FakeLcuServer.StartAsync();
        await _lcu.WriteLockfileAsync(Path.Combine(_dir, "lockfile"));
    }

    public async Task DisposeAsync()
    {
        await _lcu.DisposeAsync();
        Directory.Delete(_dir, recursive: true);
    }

    private SpikeOptions Options(int retryCount = 10) => new()
    {
        LockfilePath = Path.Combine(_dir, "lockfile"),
        OutputRoot = Path.Combine(_dir, "captures"),
        RetryCount = retryCount,
        RetryDelay = TimeSpan.FromMilliseconds(20),
        StopAfterCaptures = 1,
        Log = line => { lock (_log) _log.Add(line); },
    };

    private async Task<IReadOnlyList<string>> RunWithEventAsync(SpikeOptions options, string uri, string eventType, string data)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var run = SpikeRunner.RunAsync(options, cts.Token);
        await _lcu.WaitSubscribedAsync();
        await _lcu.PushEventAsync(uri, eventType, data);
        return await run;
    }

    [Fact]
    public async Task Capture_les_deux_JSON_a_la_fin_de_game()
    {
        var dirs = await RunWithEventAsync(Options(), "/lol-end-of-game/v1/eog-stats-block", "Create",
            """{"gameId":7123456789}""");

        var dir = Assert.Single(dirs);
        Assert.Equal(_lcu.EogStatsBlockJson, await File.ReadAllTextAsync(Path.Combine(dir, "eog-stats-block.json")));
        Assert.Equal(_lcu.MatchHistoryJson, await File.ReadAllTextAsync(Path.Combine(dir, "match-history-game.json")));
        Assert.Contains("7123456789", Path.GetFileName(dir));
    }

    [Fact]
    public async Task Relance_l_historique_tant_qu_il_repond_404()
    {
        _lcu.MatchHistoryNotReadyCount = 3;

        var dirs = await RunWithEventAsync(Options(), "/lol-end-of-game/v1/eog-stats-block", "Create",
            """{"gameId":7123456789}""");

        Assert.True(File.Exists(Path.Combine(dirs.Single(), "match-history-game.json")));
        Assert.Equal(4, _lcu.Requests.Count(r => r.StartsWith("GET /lol-match-history/v1/games/7123456789")));
        Assert.Contains(_log, l => l.Contains("404") && l.Contains("essai 1/"));
    }

    [Fact]
    public async Task Abandonne_apres_le_nombre_d_essais_mais_garde_le_bloc_de_fin_de_game()
    {
        _lcu.MatchHistoryNotReadyCount = 100;

        var dirs = await RunWithEventAsync(Options(retryCount: 3), "/lol-end-of-game/v1/eog-stats-block", "Create",
            """{"gameId":7123456789}""");

        var dir = dirs.Single();
        Assert.True(File.Exists(Path.Combine(dir, "eog-stats-block.json")));
        Assert.False(File.Exists(Path.Combine(dir, "match-history-game.json")));
        Assert.Equal(3, _lcu.Requests.Count(r => r.StartsWith("GET /lol-match-history/")));
        Assert.Contains(_log, l => l.Contains("abandon", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Capture_aussi_quand_seule_la_phase_EndOfGame_est_annoncee()
    {
        var dirs = await RunWithEventAsync(Options(), "/lol-gameflow/v1/gameflow-phase", "Update", "\"EndOfGame\"");

        Assert.True(File.Exists(Path.Combine(dirs.Single(), "eog-stats-block.json")));
    }

    [Fact]
    public async Task Enregistre_brut_les_evenements_de_fin_de_game_et_journalise_les_uri()
    {
        var options = Options();
        await RunWithEventAsync(options, "/lol-end-of-game/v1/eog-stats-block", "Create", """{"gameId":7123456789}""");

        var events = Directory.GetFiles(options.OutputRoot, "events-*.jsonl").Single();
        var content = await File.ReadAllTextAsync(events);
        Assert.Contains("\"uri\":\"/lol-end-of-game/v1/eog-stats-block\"", content);
        Assert.Contains(_log, l => l.Contains("/lol-end-of-game/v1/eog-stats-block") && l.Contains("Create"));
        Assert.Contains(_log, l => l.Contains("200"));
    }

    [Fact]
    public async Task N_enregistre_pas_les_evenements_sans_rapport_avec_la_fin_de_game()
    {
        var options = Options();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var run = SpikeRunner.RunAsync(options, cts.Token);
        await _lcu.WaitSubscribedAsync();
        await _lcu.PushEventAsync("/lol-chat/v1/friends", "Update", "{}");
        await _lcu.PushEventAsync("/lol-end-of-game/v1/eog-stats-block", "Create", """{"gameId":7123456789}""");
        await run;

        var content = await File.ReadAllTextAsync(Directory.GetFiles(options.OutputRoot, "events-*.jsonl").Single());
        Assert.DoesNotContain("lol-chat", content);
        Assert.Contains(_log, l => l.Contains("/lol-chat/v1/friends"));
    }

    [Fact]
    public async Task Refuse_clairement_un_mauvais_mot_de_passe()
    {
        await File.WriteAllTextAsync(Path.Combine(_dir, "lockfile"), $"LeagueClient:1:{_lcu.Port}:faux:https");

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ex = await Assert.ThrowsAnyAsync<Exception>(() => SpikeRunner.RunAsync(Options(), cts.Token));

        Assert.Contains("401", ex.Message);
    }
}
