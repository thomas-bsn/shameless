using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shameless.Infrastructure.Data;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>Les Séries existantes reçoivent chacune un code Companion long et distinct.</summary>
public class CodesCompanionMigrationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Migration = "CodesCompanion";

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
    public async Task Chaque_serie_existante_recoit_un_code_companion_long_et_distinct()
    {
        await _db.Database.ExecuteSqlRawAsync("""
            INSERT INTO best_of_series (id, played_at, format) VALUES (1, '2026-09-01 21:00', 3), (2, '2026-09-02 21:00', 1);
            """, Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT s.code_companion FROM best_of_series s
            WHERE NOT EXISTS (SELECT 1 FROM liens_acces l WHERE l.secret = s.code_companion)
            ORDER BY s.id
            """;
        await using var reader = await command.ExecuteReaderAsync(Ct);
        var codes = new List<string>();
        while (await reader.ReadAsync(Ct))
            codes.Add(reader.GetString(0));
        await connection.CloseAsync();

        Assert.Equal(2, codes.Count);
        Assert.All(codes, c => Assert.True(c.Length >= 40, $"Code trop court : {c}"));
        Assert.NotEqual(codes[0], codes[1]);
    }
}
