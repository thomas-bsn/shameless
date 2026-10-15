using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shameless.Infrastructure.Data;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>Les Séries existantes reçoivent leurs trois liens d'accès, chacun un secret long et distinct.</summary>
public class LiensAccesMigrationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Migration = "CapitainesEtLiensAcces";

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
    public async Task Chaque_serie_existante_recoit_deux_liens_capitaine_et_un_lien_spectateur_non_envoye()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO best_of_series (id, played_at, format) VALUES (1, '2026-09-01 21:00', 3), (2, '2026-09-02 21:00', 1);
            """, Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT l.series_id, l.portee || coalesce(l.equipe, ''), l.secret, s.liens_capitaine_envoyes
            FROM liens_acces l JOIN best_of_series s ON s.id = l.series_id
            ORDER BY l.series_id, l.portee, l.equipe
            """;
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var liens = new List<(int Serie, string Portee, string Secret, bool Envoyes)>();
        while (await reader.ReadAsync(Ct))
            liens.Add((reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetBoolean(3)));
        await connection.CloseAsync();

        Assert.Equal(
            [(1, "capitaineA"), (1, "capitaineB"), (1, "spectateur"), (2, "capitaineA"), (2, "capitaineB"), (2, "spectateur")],
            liens.Select(l => (l.Serie, l.Portee)));
        Assert.All(liens, l => Assert.True(l.Secret.Length >= 40));
        Assert.Equal(liens.Count, liens.Select(l => l.Secret).Distinct().Count());
        Assert.All(liens, l => Assert.False(l.Envoyes));
    }
}
