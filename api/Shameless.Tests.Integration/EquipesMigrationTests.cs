using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shameless.Infrastructure.Data;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Conversion des données existantes : le vainqueur d'une game passe d'un Side à une Équipe,
/// à partir de la référence de RiotIds de l'Équipe A.
/// </summary>
public class EquipesMigrationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Migration = "EquipesExplicites";

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
    public async Task Les_vainqueurs_existants_sont_convertis_de_side_en_equipe()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO players (id, prenom, riot_id, puuid) VALUES
                (1, 'A1', 'A1#EUW', 'p1'), (2, 'A2', 'A2#EUW', 'p2'), (3, 'B1', 'B1#EUW', 'p3'), (4, 'B2', 'B2#EUW', 'p4');

            -- Série 1 : référence à jour, l'Équipe A change de Side, compteurs faux à recalculer
            -- Série 2 : référence périmée (RiotId changé depuis), l'Équipe A est le bleu de la première game
            INSERT INTO best_of_series (id, played_at, format, team_a_wins, team_b_wins, team_a_reference) VALUES
                (1, '2026-09-01 21:00', 3, 0, 0, '["A1#EUW","A2#EUW"]'),
                (2, '2026-09-02 21:00', 3, 0, 0, '["Ancien#EUW"]');

            INSERT INTO custom_games (id, played_at, winner, status, series_id) VALUES
                (10, '2026-09-01 21:00', 'blue', 'completed', 1),
                (11, '2026-09-01 21:40', 'red',  'completed', 1),
                (12, '2026-09-01 22:20', 'blue', 'completed', 1),
                (13, '2026-09-01 23:00', '',     'pending',   1),
                (20, '2026-09-02 21:00', 'red',  'completed', 2),
                (21, '2026-09-02 21:40', 'red',  'completed', 2),
                (30, '2026-09-03 21:00', 'red',  'completed', NULL);

            INSERT INTO custom_game_participants (custom_game_id, player_id, team) VALUES
                (10, 1, 'blue'), (10, 2, 'blue'), (10, 3, 'red'),  (10, 4, 'red'),
                (11, 1, 'red'),  (11, 2, 'red'),  (11, 3, 'blue'), (11, 4, 'blue'),
                (12, 1, 'red'),  (12, 2, 'red'),  (12, 3, 'blue'), (12, 4, 'blue'),
                (13, 1, 'blue'), (13, 2, 'blue'), (13, 3, 'red'),  (13, 4, 'red'),
                (20, 3, 'blue'), (20, 4, 'blue'), (20, 1, 'red'),  (20, 2, 'red'),
                (21, 3, 'red'),  (21, 4, 'red'),  (21, 1, 'blue'), (21, 2, 'blue'),
                (30, 1, 'blue'), (30, 3, 'red');
            """, Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        var games = await QueryAsync(
            "SELECT id, team_a_side, coalesce(winning_team, '-') FROM custom_games ORDER BY id",
            r => (r.GetInt32(0), r.GetString(1), r.GetString(2)));
        Assert.Equal(
            [
                (10, "blue", "A"), // A en bleu, bleu gagne
                (11, "red", "A"),  // A en rouge, rouge gagne
                (12, "red", "B"),  // A en rouge, bleu gagne
                (13, "blue", "-"), // en attente
                (20, "blue", "B"), // série 2 : A = bleu de la première game (B1, B2)
                (21, "red", "A"),
                (30, "blue", "B")  // game hors série : A = bleu
            ],
            games);

        var series = await QueryAsync(
            "SELECT id, team_a_wins, team_b_wins FROM best_of_series ORDER BY id",
            r => (r.GetInt32(0), r.GetInt32(1), r.GetInt32(2)));
        Assert.Equal([(1, 2, 1), (2, 1, 1)], series);
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
