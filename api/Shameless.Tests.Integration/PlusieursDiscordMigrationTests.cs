using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shameless.Infrastructure.Data;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>Les liens Utilisateur-Joueur existants passent côté Utilisateur sans rien perdre (ADR 0018).</summary>
public class PlusieursDiscordMigrationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Migration = "PlusieursDiscordParJoueur";

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
    public async Task Les_liens_existants_deviennent_le_lien_principal_de_l_utilisateur()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO users (id, discord_id, discord_username) VALUES (1, 11, 'lie'), (2, 22, 'libre');
            INSERT INTO players (id, puuid, prenom, riot_id, created_at, current_cost, is_placement, peak_lp, user_id)
            VALUES (7, 'p7', 'Alice', 'Alice#EUW', now(), 3, false, 0, 1), (8, 'p8', 'Bob', 'Bob#EUW', now(), 3, false, 0, NULL);
            """, Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        Assert.Equal(["lie:7:true:1"], await RowsAsync(
            "SELECT u.discord_username || ':' || u.player_id || ':' || (u.player_linked_at IS NOT NULL) || ':' || p.user_id FROM users u JOIN players p ON p.id = u.player_id"));
        Assert.Equal(["libre"], await RowsAsync("SELECT discord_username FROM users WHERE player_id IS NULL AND player_linked_at IS NULL"));
    }

    private async Task<List<string>> RowsAsync(string sql)
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
