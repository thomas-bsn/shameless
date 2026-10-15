using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shameless.Application.BackgroundJobs;
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
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

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
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

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

    private static Task TickAsync(IServiceProvider services) =>
        services.GetServices<IHostedService>().OfType<FermetureAutomatiqueSoireeJob>().Single().FermerSiInactiveAsync();

    private static async Task<int?> LireSoireeOuverteAsync(HttpClient client)
    {
        var soiree = (await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct))).GetProperty("soiree");
        return soiree.ValueKind == JsonValueKind.Null ? null : soiree.GetProperty("id").GetInt32();
    }
}
