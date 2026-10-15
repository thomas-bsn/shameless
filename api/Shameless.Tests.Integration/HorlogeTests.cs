using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Shameless.Application.Interfaces;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class HorlogeTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Une_game_et_sa_serie_sont_datees_par_l_horloge_de_l_api()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Clock.SetUtcNow(new DateTimeOffset(2025, 3, 14, 21, 30, 0, TimeSpan.Zero));
        var created = await CreateSerieAsync(client, format: 3);
        await PlayAndWinAsync(client, created.SeriesId, "A");

        var history = await ReadJsonAsync(await client.GetAsync("/games/history/2025/3", Ct));
        var serie = Assert.Single(history.GetProperty("games").EnumerateArray());
        Assert.Equal(created.SeriesId, serie.GetProperty("seriesId").GetInt32());
        Assert.Equal(new DateTime(2025, 3, 14, 21, 30, 0), serie.GetProperty("playedAt").GetDateTime());
        var game = Assert.Single(serie.GetProperty("games").EnumerateArray());
        Assert.Equal(new DateTime(2025, 3, 14, 21, 30, 0), game.GetProperty("playedAt").GetDateTime());
    }

    [Fact]
    public async Task L_horloge_avance_a_la_demande()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Clock.SetUtcNow(new DateTimeOffset(2025, 3, 31, 23, 0, 0, TimeSpan.Zero));
        var serie = await CreateSerieAsync(client, format: 3);
        Factory.Clock.Advance(TimeSpan.FromHours(2));

        await PlayAndWinAsync(client, serie.SeriesId, "A");

        var history = await ReadJsonAsync(await client.GetAsync("/games/history/2025/4", Ct));
        var game = Assert.Single(Assert.Single(history.GetProperty("games").EnumerateArray()).GetProperty("games").EnumerateArray());
        Assert.Equal(new DateTime(2025, 4, 1, 1, 0, 0), game.GetProperty("playedAt").GetDateTime());
    }

    [Fact]
    public async Task L_alea_suit_une_graine_fixe_rejouee_a_chaque_test()
    {
        var alea = Factory.Services.GetRequiredService<IRandomSource>();
        var premierTirage = Enumerable.Range(0, 10).Select(_ => alea.Next(1000)).ToArray();

        await Factory.ResetAsync();
        var secondTirage = Enumerable.Range(0, 10).Select(_ => alea.Next(1000)).ToArray();

        Assert.Equal(premierTirage, secondTirage);
        Assert.True(premierTirage.Distinct().Count() > 1);
    }
}
