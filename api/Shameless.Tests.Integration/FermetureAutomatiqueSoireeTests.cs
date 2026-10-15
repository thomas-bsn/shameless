using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shameless.Application.BackgroundJobs;
using Shameless.Application.Entities;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class FermetureAutomatiqueSoireeTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Une_soiree_sans_game_est_fermee_trois_heures_apres_son_ouverture()
    {
        var client = Factory.CreateSignedInClient();
        await OuvrirSoireeAsync(client);

        Factory.Clock.Advance(TimeSpan.FromHours(3) + TimeSpan.FromMinutes(1));
        await TickAsync(Factory.Services);

        Assert.Null(await LireSoireeOuverteAsync(client));
    }

    [Fact]
    public async Task Une_soiree_n_est_pas_fermee_avant_le_delai()
    {
        var client = Factory.CreateSignedInClient();
        var id = await OuvrirSoireeAsync(client);

        Factory.Clock.Advance(TimeSpan.FromHours(3) - TimeSpan.FromMinutes(1));
        await TickAsync(Factory.Services);

        Assert.Equal(id, await LireSoireeOuverteAsync(client));
    }

    [Fact]
    public async Task Le_delai_court_depuis_la_derniere_game_terminee_et_non_depuis_l_ouverture()
    {
        var client = Factory.CreateSignedInClient();
        var id = await OuvrirSoireeAsync(client);
        Factory.Clock.Advance(TimeSpan.FromHours(2));
        await AjouterSerieTermineeAsync(id, Factory.Clock.GetUtcNow().UtcDateTime);

        Factory.Clock.Advance(TimeSpan.FromHours(2));
        await TickAsync(Factory.Services);
        Assert.Equal(id, await LireSoireeOuverteAsync(client));

        Factory.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));
        await TickAsync(Factory.Services);
        Assert.Null(await LireSoireeOuverteAsync(client));
    }

    [Fact]
    public async Task Une_soiree_avec_une_serie_en_cours_n_est_jamais_fermee_automatiquement()
    {
        var client = Factory.CreateSignedInClient();
        var id = await OuvrirSoireeAsync(client);
        await AjouterSerieEnCoursAsync(id, Factory.Clock.GetUtcNow().UtcDateTime);

        Factory.Clock.Advance(TimeSpan.FromHours(12));
        await TickAsync(Factory.Services);

        Assert.Equal(id, await LireSoireeOuverteAsync(client));
    }

    [Fact]
    public async Task Le_delai_se_configure()
    {
        var client = Factory.CreateSignedInClient();
        await using var uneHeure = Factory.WithWebHostBuilder(b => b.UseSetting("Soiree:AutoCloseAfter", "01:00:00"));
        var id = await OuvrirSoireeAsync(client);

        Factory.Clock.Advance(TimeSpan.FromMinutes(59));
        await TickAsync(uneHeure.Services);
        Assert.Equal(id, await LireSoireeOuverteAsync(client));

        Factory.Clock.Advance(TimeSpan.FromMinutes(2));
        await TickAsync(uneHeure.Services);
        Assert.Null(await LireSoireeOuverteAsync(client));
    }

    private async Task<int> OuvrirSoireeAsync(HttpClient client) =>
        (await ReadJsonAsync(await client.PostAsync("/soirees", null, Ct))).GetProperty("id").GetInt32();

    // Aucune route ne rattache encore une Série à la Soirée : on les pose directement en base.

    /// <summary>BO3 gagnée 2-0 par l'équipe A, dernière game terminée à <paramref name="derniereGame"/>.</summary>
    private Task AjouterSerieTermineeAsync(int soireeId, DateTime derniereGame) =>
        AjouterSerieAsync(soireeId, teamAWins: 2,
            new CustomGame { PlayedAt = derniereGame.AddMinutes(-40), CreatedAt = derniereGame, WinningTeam = Equipes.A, Status = "completed" },
            new CustomGame { PlayedAt = derniereGame, CreatedAt = derniereGame, WinningTeam = Equipes.A, TeamASide = Sides.Red, Status = "completed" });

    /// <summary>BO3 à 1-0, deuxième game pas encore jouée.</summary>
    private Task AjouterSerieEnCoursAsync(int soireeId, DateTime premiereGame) =>
        AjouterSerieAsync(soireeId, teamAWins: 1,
            new CustomGame { PlayedAt = premiereGame, CreatedAt = premiereGame, WinningTeam = Equipes.A, Status = "completed" });

    private async Task AjouterSerieAsync(int soireeId, int teamAWins, params CustomGame[] games)
    {
        await using var db = Factory.CreateDbContext();
        db.BestOfSeries.Add(new BestOfSeries
        {
            SoireeId = soireeId,
            PlayedAt = games[0].PlayedAt,
            Format = 3,
            TeamAWins = teamAWins,
            Games = games
        });
        await db.SaveChangesAsync(Ct);
    }

    private static Task TickAsync(IServiceProvider services) =>
        services.GetServices<IHostedService>().OfType<FermetureAutomatiqueSoireeJob>().Single().FermerSiInactiveAsync();

    private static async Task<int?> LireSoireeOuverteAsync(HttpClient client)
    {
        var soiree = (await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct))).GetProperty("soiree");
        return soiree.ValueKind == JsonValueKind.Null ? null : soiree.GetProperty("id").GetInt32();
    }
}
