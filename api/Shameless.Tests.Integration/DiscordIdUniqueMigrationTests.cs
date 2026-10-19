using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shameless.Infrastructure.Data;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// <c>users.discord_id</c> devient unique, sans casser une base qui contiendrait déjà des doublons :
/// l'index n'est alors pas créé et la migration passe.
/// </summary>
public class DiscordIdUniqueMigrationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Migration = "DiscordIdUniqueDesUtilisateurs";
    private const string Index = "ix_users_discord_id";

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private readonly string _connectionString = new NpgsqlConnectionStringBuilder(factory.ConnectionString)
    {
        Database = $"mig_{Guid.NewGuid():N}"
    }.ConnectionString;

    private AppDbContext _db = null!;

    public async ValueTask InitializeAsync()
    {
        _db = factory.CreateDbContext(_connectionString);
        var previous = _db.Database.GetMigrations().TakeWhile(m => !m.EndsWith($"_{Migration}")).Last();
        await _db.GetService<IMigrator>().MigrateAsync(previous, Ct);
    }

    public async ValueTask DisposeAsync()
    {
        await _db.Database.EnsureDeletedAsync();
        await _db.DisposeAsync();
    }

    [Fact]
    public async Task Sans_doublon_l_index_unique_est_cree()
    {
        await _db.Database.ExecuteSqlRawAsync("INSERT INTO users (discord_id) VALUES (1), (2);", Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        Assert.True(await IndexExisteAsync());
        await Assert.ThrowsAsync<PostgresException>(() =>
            _db.Database.ExecuteSqlRawAsync("INSERT INTO users (discord_id) VALUES (1);", Ct));
    }

    [Fact]
    public async Task Avec_des_doublons_la_migration_passe_sans_creer_l_index()
    {
        await _db.Database.ExecuteSqlRawAsync("INSERT INTO users (discord_id) VALUES (1), (1);", Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        Assert.False(await IndexExisteAsync());
        Assert.Equal(2, await _db.Database.SqlQueryRaw<int>("SELECT count(*)::int AS \"Value\" FROM users").SingleAsync(Ct));
    }

    [Fact]
    public async Task La_migration_passe_quand_l_index_existe_deja()
    {
        await _db.Database.ExecuteSqlRawAsync($"CREATE UNIQUE INDEX {Index} ON users (discord_id);", Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        Assert.True(await IndexExisteAsync());
    }

    private async Task<bool> IndexExisteAsync() =>
        await _db.Database.SqlQueryRaw<int>($"SELECT count(*)::int AS \"Value\" FROM pg_indexes WHERE indexname = '{Index}'").SingleAsync(Ct) == 1;
}
