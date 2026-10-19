using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shameless.Infrastructure.Data;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>Les séries en cours au déploiement comptent comme déjà rejointes, pour ne pas bloquer une draft.</summary>
public class PresenceDesCapitainesMigrationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Migration = "PresenceDesCapitaines";

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
    public async Task Les_capitaines_d_une_serie_en_cours_ont_deja_rejoint_pas_ceux_d_une_serie_terminee_ni_le_lien_spectateur()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO best_of_series (id, played_at, format, outcome, code_companion) VALUES
                (1, '2026-09-01 21:00', 3, 'en_cours', 'c1'), (2, '2026-09-02 21:00', 1, 'gagnee', 'c2');
            INSERT INTO liens_acces (series_id, portee, equipe, secret) VALUES
                (1, 'capitaine', 'A', 'a1'), (1, 'capitaine', 'B', 'b1'), (1, 'spectateur', NULL, 's1'),
                (2, 'capitaine', 'A', 'a2');
            """, Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT secret, rejoint_le IS NOT NULL FROM liens_acces ORDER BY secret";
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var rejoints = new Dictionary<string, bool>();
        while (await reader.ReadAsync(Ct))
            rejoints[reader.GetString(0)] = reader.GetBoolean(1);
        await connection.CloseAsync();

        Assert.Equal(
            new Dictionary<string, bool> { ["a1"] = true, ["b1"] = true, ["s1"] = false, ["a2"] = false },
            rejoints);
    }
}
