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

    public FakeDiscord Discord { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public string ConnectionString => _postgres.GetConnectionString();

    public AppDbContext CreateDbContext(string? connectionString = null) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connectionString ?? _postgres.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options);

    public async Task ResetAsync()
    {
        Riot.Reset();
        Discord.Reset();
        await using var db = CreateDbContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE custom_game_participants, custom_games, best_of_series, player_monthly_costs, players, users RESTART IDENTITY CASCADE");
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

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRiotService>();
            services.AddSingleton<IRiotService>(Riot);
            services.RemoveAll<IDiscordOAuthClient>();
            services.AddSingleton<IDiscordOAuthClient>(Discord);
            services.AddFakeLogging();
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
