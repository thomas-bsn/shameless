using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shameless.Infrastructure.Data;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>Les games déjà terminées reçoivent leur fin : faute de mieux, leur lancement.</summary>
public class FinDesGamesMigrationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Migration = "FinDesGames";

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
    public async Task Les_games_terminees_finissent_a_leur_lancement_les_games_en_attente_pas_encore()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO best_of_series (id, played_at, format) VALUES (1, '2026-09-01 21:00', 3);
            INSERT INTO custom_games (id, played_at, team_a_side, winning_team, status, series_id) VALUES
                (10, '2026-09-01 21:00', 'blue', 'A', 'completed', 1),
                (11, '2026-09-01 21:40', 'red',  NULL, 'pending', 1);
            """, Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT id, coalesce(completed_at::text, '-') FROM custom_games ORDER BY id";
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var games = new List<(int, string)>();
        while (await reader.ReadAsync(Ct))
            games.Add((reader.GetInt32(0), reader.GetString(1)));
        await connection.CloseAsync();

        Assert.Equal([(10, "2026-09-01 21:00:00"), (11, "-")], games);
    }
}
