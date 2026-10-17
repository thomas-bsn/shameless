using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Testing;
using Shameless.Api.Interfaces;
using Shameless.Application.Interfaces;
using Shameless.Infrastructure.Data;
using Shameless.Infrastructure.Integrations.Bot;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(Shameless.Tests.Integration.Infrastructure.ApiFactory))]

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>
/// L'API hébergée en mémoire, branchée sur un Postgres jetable migré par la Baseline.
/// Riot est simulé à la frontière.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16").Build();

    public FakeRiotService Riot { get; } = new();

    public string ConnectionString => _postgres.GetConnectionString();

    public FakeDiscord Discord { get; } = new();

    public FakeChampionCatalog Champions { get; } = new();

    /// <summary>Le bot Discord, simulé à la frontière HTTP (candidats vocaux, webhooks).</summary>
    public FakeBot Bot { get; } = new();

    /// <summary>Horloge de l'API : réglable (<c>SetUtcNow</c>) et avançable (<c>Advance</c>). Remise à l'heure réelle avant chaque test.</summary>
    public FakeClock Clock { get; } = new();

    /// <summary>Aléa de l'API, graine fixe rejouée depuis le début avant chaque test.</summary>
    public SeededRandomSource Random { get; } = new(42);

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public AppDbContext CreateDbContext(string? connectionString = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString ?? _postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options);

    /// <summary>Le stockage des clips : un dossier temporaire propre à la suite, vidé avant chaque test.</summary>
    public string DossierDesClips { get; } = Path.Combine(Path.GetTempPath(), "shameless-clips-" + Guid.NewGuid().ToString("N"));

    /// <summary>Taille maximale d'un clip en test, petite pour exercer le refus.</summary>
    public const long TailleMaxDesClips = 64 * 1024;

    public async Task ResetAsync()
    {
        if (Directory.Exists(DossierDesClips)) Directory.Delete(DossierDesClips, recursive: true);
        Riot.Reset();
        Discord.Reset();
        Champions.Reset();
        Bot.Reset();
        Clock.Reset();
        Random.Reset();
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE clips, bulletins_de_vote, spectateurs_de_game, evenements_bot, rapports_companion, drafts_champions_actions, drafts_champions, liens_acces, picker_session_players, picker_sessions, custom_game_participants, custom_games, series_players, best_of_series, soirees, player_monthly_costs, players, users RESTART IDENTITY CASCADE");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Shameless", _postgres.GetConnectionString());
        builder.UseSetting("Auth:Discord:ClientId", "test-client");
        builder.UseSetting("Auth:Discord:ClientSecret", "test-secret");
        builder.UseSetting("Auth:Discord:RedirectUri", "http://localhost/api/auth/discord/callback");
        builder.UseSetting("Auth:Discord:AllowedGuildId", TestSession.GuildId);
        builder.UseSetting("Auth:AllowedReturnOrigins", TestSession.AllowedOrigin);
        builder.UseSetting("Auth:AdminDiscordIds", TestSession.AdminDiscordId);
        // Pas de clé Riot : l'instance doit démarrer, job de rangs désactivé. Riot est simulé.
        builder.UseSetting("Auth:Riot:ApiKey", "");
        builder.UseSetting("Bot:ApiKey", TestSession.BotKey);
        builder.UseSetting("Bot:Url", FakeBot.Url);
        builder.UseSetting("Bot:OutboundApiKey", FakeBot.ApiKey);
        builder.UseSetting("Bot:WebhookSecret", FakeBot.WebhookSecret);
        builder.UseSetting("Bot:WebUrl", TestSession.AllowedOrigin);
        // Les tests publient eux-mêmes les webhooks (PublierAsync) : le job ne tourne pas seul.
        builder.UseSetting("Bot:Publication:Interval", "40.00:00:00");
        builder.UseSetting("Clips:StoragePath", DossierDesClips);
        builder.UseSetting("Clips:MaxFileSizeBytes", TailleMaxDesClips.ToString(System.Globalization.CultureInfo.InvariantCulture));
        builder.UseSetting("Instance:TimeZone", "Europe/Paris");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRiotService>();
            services.AddSingleton<IRiotService>(Riot);
            services.RemoveAll<IDiscordOAuthClient>();
            services.AddSingleton<IDiscordOAuthClient>(Discord);
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
            services.RemoveAll<IRandomSource>();
            services.AddSingleton<IRandomSource>(Random);
            services.RemoveAll<IChampionCatalog>();
            services.AddSingleton<IChampionCatalog>(Champions);
            services.AddHttpClient<IBotDiscord, BotDiscordClient>().ConfigurePrimaryHttpMessageHandler(() => Bot);
            services.AddFakeLogging();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
        if (Directory.Exists(DossierDesClips)) Directory.Delete(DossierDesClips, recursive: true);
    }
}
