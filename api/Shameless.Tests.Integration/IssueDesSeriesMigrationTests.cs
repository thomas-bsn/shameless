using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shameless.Infrastructure.Data;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Conversion des séries existantes : issue calculée à partir du score, composition courante des
/// Équipes reprise de la dernière game, games en attente des séries interrompues supprimées (ADR 0009).
/// </summary>
public class IssueDesSeriesMigrationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Migration = "IssueEtCompositionDesSeries";

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
    public async Task Les_series_existantes_recoivent_leur_issue_et_la_composition_de_leur_derniere_game()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO players (id, prenom, riot_id, puuid) VALUES
                (1, 'A1', 'A1#EUW', 'p1'), (2, 'B1', 'B1#EUW', 'p2'), (3, 'C1', 'C1#EUW', 'p3');

            INSERT INTO best_of_series (id, played_at, format, team_a_wins, team_b_wins) VALUES
                (1, '2026-09-01 21:00', 3, 1, 2),  -- gagnée par B
                (2, '2026-09-02 21:00', 2, 1, 1),  -- BO2 nul
                (3, '2026-09-03 21:00', 5, 1, 0),  -- jamais allée au bout
                (4, '2026-09-04 21:00', 1, 1, 0);  -- gagnée par A

            INSERT INTO custom_games (id, played_at, team_a_side, winning_team, status, series_id) VALUES
                (10, '2026-09-01 21:00', 'blue', 'A', 'completed', 1),
                (11, '2026-09-01 21:40', 'red',  'B', 'completed', 1),
                (12, '2026-09-01 22:20', 'red',  'B', 'completed', 1),
                (30, '2026-09-03 21:00', 'blue', 'A', 'completed', 3),
                (31, '2026-09-03 21:40', 'red',  NULL, 'pending', 3),
                (40, '2026-09-04 21:00', 'blue', 'A', 'completed', 4);

            -- Remplacement en cours de série 1 : C1 prend la place de A1 à la dernière game.
            INSERT INTO custom_game_participants (custom_game_id, player_id, team) VALUES
                (10, 1, 'blue'), (10, 2, 'red'),
                (11, 1, 'red'),  (11, 2, 'blue'),
                (12, 3, 'red'),  (12, 2, 'blue'),
                (30, 1, 'blue'), (30, 2, 'red'),
                (31, 1, 'red'),  (31, 2, 'blue'),
                (40, 1, 'blue'), (40, 2, 'red');
            """, Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        var series = await QueryAsync(
            "SELECT id, outcome, coalesce(winning_team, '-'), fearless FROM best_of_series ORDER BY id",
            r => (r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetBoolean(3)));
        Assert.Equal(
            [
                (1, "gagnee", "B", false),
                (2, "nulle", "-", false),
                (3, "interrompue", "-", false),
                (4, "gagnee", "A", false)
            ],
            series);

        var players = await QueryAsync(
            "SELECT series_id, player_id, equipe FROM series_players ORDER BY series_id, player_id",
            r => (r.GetInt32(0), r.GetInt32(1), r.GetString(2)));
        Assert.Equal([(1, 2, "B"), (1, 3, "A"), (3, 1, "A"), (3, 2, "B"), (4, 1, "A"), (4, 2, "B")], players);

        var games = await QueryAsync("SELECT id FROM custom_games ORDER BY id", r => r.GetInt32(0));
        Assert.Equal([10, 11, 12, 30, 40], games);
    }

    private async Task<List<T>> QueryAsync<T>(string sql, Func<System.Data.Common.DbDataReader, T> read)
    {
        var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync(Ct);
        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(Ct);
            var rows = new List<T>();
            while (await reader.ReadAsync(Ct))
                rows.Add(read(reader));
            return rows;
        }
        finally
        {
            await connection.CloseAsync();
        }
    }
}
