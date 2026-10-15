using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Shameless.Api.Interfaces;
using Shameless.Application.BackgroundJobs;
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

    public AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_postgres.GetConnectionString())
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
        var connection = new NpgsqlConnectionStringBuilder(_postgres.GetConnectionString());

        builder.UseEnvironment("Testing");
        builder.UseSetting("Database:Host", connection.Host);
        builder.UseSetting("Database:Port", connection.Port.ToString());
        builder.UseSetting("Database:Database", connection.Database);
        builder.UseSetting("Database:Username", connection.Username);
        builder.UseSetting("Database:Password", connection.Password);
        builder.UseSetting("Auth:Discord:ClientId", "test-client");
        builder.UseSetting("Auth:Discord:ClientSecret", "test-secret");
        builder.UseSetting("Auth:Discord:RedirectUri", "http://localhost/api/auth/discord/callback");
        builder.UseSetting("Auth:Discord:AllowedGuildId", TestSession.GuildId);
        builder.UseSetting("Auth:AllowedReturnOrigins", TestSession.AllowedOrigin);
        builder.UseSetting("Auth:AdminDiscordIds", TestSession.AdminDiscordId);
        builder.UseSetting("Auth:Riot:ApiKey", "test-riot-key");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IRiotService>();
            services.AddSingleton<IRiotService>(Riot);
            services.RemoveAll<IDiscordOAuthClient>();
            services.AddSingleton<IDiscordOAuthClient>(Discord);

            var job = services.Single(d => d.ImplementationType == typeof(RefreshRanksJob));
            services.Remove(job);
        });
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}
