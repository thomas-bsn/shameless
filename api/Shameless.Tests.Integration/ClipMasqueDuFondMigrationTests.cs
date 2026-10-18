using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using Shameless.Infrastructure.Data;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Une base qui a servi une ancienne version de la refonte du front peut déjà porter la colonne
/// <c>clips.masque_du_fond</c> : la migration ne doit pas échouer dessus.
/// </summary>
public class ClipMasqueDuFondMigrationTests(ApiFactory factory) : IAsyncLifetime
{
    private const string Migration = "ClipMasqueDuFond";

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
    public async Task La_migration_passe_quand_la_colonne_existe_deja()
    {
        await _db.Database.ExecuteSqlRawAsync(
            "ALTER TABLE clips ADD COLUMN masque_du_fond boolean NOT NULL DEFAULT FALSE;", Ct);

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        Assert.True(await ColonneExisteAsync());
    }

    [Fact]
    public async Task La_migration_ajoute_la_colonne_sur_une_base_qui_ne_l_a_pas()
    {
        Assert.False(await ColonneExisteAsync());

        await _db.GetService<IMigrator>().MigrateAsync(cancellationToken: Ct);

        Assert.True(await ColonneExisteAsync());
    }

    private async Task<bool> ColonneExisteAsync()
    {
        var connection = _db.Database.GetDbConnection();
        await connection.OpenAsync(Ct);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT count(*) FROM information_schema.columns
            WHERE table_name = 'clips' AND column_name = 'masque_du_fond'
            """;
        var count = (long)(await command.ExecuteScalarAsync(Ct))!;
        await connection.CloseAsync();
        return count == 1;
    }
}
