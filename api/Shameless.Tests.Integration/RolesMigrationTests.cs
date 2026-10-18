using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shameless.Infrastructure.Data;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Les colonnes de rôle de <c>users</c> s'ajoutent sans échouer sur une base qui les porte déjà
/// (ancienne tentative), et les Utilisateurs existants deviennent des « utilisateur ».
/// </summary>
public class RolesMigrationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Migration = "RolesDesUtilisateurs";

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
    public async Task Les_utilisateurs_existants_deviennent_des_utilisateurs_simples()
    {
        await _db.Database.ExecuteSqlRawAsync("INSERT INTO users (discord_id, discord_username) VALUES (1, 'a');", Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        Assert.Equal(["Utilisateur"], await ScalarsAsync("SELECT role FROM users"));
        Assert.Equal(3, (await ScalarsAsync("SELECT column_name FROM information_schema.columns WHERE table_name = 'users' AND column_name IN ('role','promoted_by','promoted_at')")).Count);
    }

    [Fact]
    public async Task La_migration_passe_quand_les_colonnes_existent_deja_et_garde_leurs_valeurs()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            ALTER TABLE users ADD COLUMN role character varying(30) NOT NULL DEFAULT 'Utilisateur';
            ALTER TABLE users ADD COLUMN promoted_by character varying NULL;
            ALTER TABLE users ADD COLUMN promoted_at timestamp without time zone NULL;
            INSERT INTO users (discord_id, discord_username, role, promoted_by) VALUES (1, 'a', 'Administrateur', 'moi');
            """, Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        Assert.Equal(["Administrateur"], await ScalarsAsync("SELECT role FROM users"));
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
