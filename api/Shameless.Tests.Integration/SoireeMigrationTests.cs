using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shameless.Application.Entities;
using Shameless.Infrastructure.Data;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// La migration des Soirées part d'une base au schéma précédent, contenant des séries
/// et des games déjà jouées, et reconstitue des Soirées fermées par nuit (coupure à 6 h, heure de Paris).
/// </summary>
public class SoireeMigrationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string MigrationSoirees = "Soirees";

    private readonly string _connectionString = new NpgsqlConnectionStringBuilder(factory.ConnectionString)
    {
        Database = $"mig_{Guid.NewGuid():N}"
    }.ConnectionString;

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private AppDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_connectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.EnsureDeletedAsync();
    }

    [Fact]
    public async Task Les_series_existantes_sont_regroupees_en_soirees_fermees_par_nuit_avec_coupure_a_6h()
    {
        await using var db = CreateDbContext();
        var migrator = db.GetService<IMigrator>();
        var migrations = db.Database.GetMigrations().ToList();
        var previous = migrations[migrations.FindIndex(m => m.EndsWith("_" + MigrationSoirees)) - 1];
        await migrator.MigrateAsync(previous, Ct);

        // Heures UTC stockées telles quelles. Été (UTC+2) : 21:00 = 23 h, 23:00 = 1 h, 05:00 = 7 h à Paris.
        // Hiver (UTC+1) : 22:30 = 23 h 30, 04:30 = 5 h 30 à Paris.
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO best_of_series (id, played_at, format, team_a_wins, team_b_wins, team_a_reference) VALUES
              (1, '2026-07-10 21:00:00', 1, 1, 0, '[]'),
              (2, '2026-07-10 23:00:00', 3, 2, 1, '[]'),
              (3, '2026-07-11 05:00:00', 1, 0, 1, '[]'),
              (4, '2026-01-15 22:30:00', 1, 1, 0, '[]'),
              (5, '2026-01-16 04:30:00', 1, 0, 1, '[]');
            SELECT setval(pg_get_serial_sequence('best_of_series', 'id'), 5);
            INSERT INTO custom_games (played_at, winner, status, series_id) VALUES
              ('2026-07-10 21:05:00', 'blue', 'completed', 1),
              ('2026-07-10 23:05:00', 'blue', 'completed', 2),
              ('2026-07-10 23:40:00', 'red', 'completed', 2),
              ('2026-07-11 05:05:00', 'red', 'completed', 3);
            """, Ct);

        await migrator.MigrateAsync(cancellationToken: Ct);

        var series = await db.BestOfSeries.AsNoTracking().OrderBy(s => s.Id).ToListAsync(Ct);
        var soirees = await db.Soirees.AsNoTracking().ToDictionaryAsync(s => s.Id, Ct);
        Assert.All(series, s => Assert.NotNull(s.SoireeId));
        Assert.Equal(3, soirees.Count);
        Assert.All(soirees.Values, s => Assert.Equal(SoireeStatus.Closed, s.Status));

        // 23 h et 1 h : même Soirée ; 7 h : la suivante.
        Assert.Equal(series[0].SoireeId, series[1].SoireeId);
        Assert.NotEqual(series[1].SoireeId, series[2].SoireeId);
        // Hiver : 23 h 30 et 5 h 30 dans la même Soirée, distincte de l'été.
        Assert.Equal(series[3].SoireeId, series[4].SoireeId);
        Assert.NotEqual(series[0].SoireeId, series[3].SoireeId);

        // La Soirée reconstituée couvre la première série et la dernière game de la nuit.
        var nuit = soirees[series[0].SoireeId!.Value];
        Assert.Equal(new DateTime(2026, 7, 10, 21, 0, 0), nuit.OpenedAt);
        Assert.Equal(new DateTime(2026, 7, 10, 23, 40, 0), nuit.ClosedAt);

        // Aucune Soirée ouverte après la migration : on peut en ouvrir une.
        db.Soirees.Add(new Soiree { Status = SoireeStatus.Open, OpenedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(Ct);
    }
}
