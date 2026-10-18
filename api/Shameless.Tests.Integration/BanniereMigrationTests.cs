using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shameless.Infrastructure.Data;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>Les colonnes de bannière de <c>users</c> s'ajoutent sans échouer sur une base qui les porte déjà.</summary>
public class BanniereMigrationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Migration = "BanniereDiscordDesUtilisateurs";

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
    public async Task La_migration_ajoute_les_colonnes_et_garde_les_utilisateurs()
    {
        await _db.Database.ExecuteSqlRawAsync("INSERT INTO users (discord_id, discord_username) VALUES (1, 'a');", Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        Assert.Equal(["a"], await ScalarsAsync("SELECT discord_username FROM users WHERE banner_url IS NULL AND banner_color IS NULL"));
    }

    [Fact]
    public async Task La_migration_passe_quand_les_colonnes_existent_deja_et_garde_leurs_valeurs()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE users ADD COLUMN banner_url character varying NULL;
            ALTER TABLE users ADD COLUMN banner_color character varying(7) NULL;
            INSERT INTO users (discord_id, discord_username, banner_url, banner_color) VALUES (1, 'a', 'https://x/b.png', '#112233');
            """, Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        Assert.Equal(["https://x/b.png"], await ScalarsAsync("SELECT banner_url FROM users"));
    }

    private async Task<List<string>> ScalarsAsync(string sql)
    {
        var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var values = new List<string>();
        while (await reader.ReadAsync(Ct))
            values.Add(reader.GetString(0));
        await connection.CloseAsync();
        return values;
    }
}
