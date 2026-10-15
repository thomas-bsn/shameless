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
        var client = Factory.CreateAdminClient();
        Factory.Clock.SetUtcNow(new DateTimeOffset(2025, 3, 14, 21, 30, 0, TimeSpan.Zero));
        var ids = await CreatePlayersAsync(client, "A1", "B1");

        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/games",
            new { blueTeam = new[] { ids[0] }, redTeam = new[] { ids[1] }, boFormat = 3 }, Ct));

        Assert.Equal(new DateTime(2025, 3, 14, 21, 30, 0), created.GetProperty("playedAt").GetDateTime());
        var gameId = created.GetProperty("gameId").GetInt32();
        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct));

        var history = await ReadJsonAsync(await client.GetAsync("/games/history/2025/3", Ct));
        var serie = Assert.Single(history.GetProperty("games").EnumerateArray());
        Assert.Equal(created.GetProperty("seriesId").GetInt32(), serie.GetProperty("seriesId").GetInt32());
    }

    [Fact]
    public async Task L_horloge_avance_a_la_demande()
    {
        var client = Factory.CreateAdminClient();
        Factory.Clock.SetUtcNow(new DateTimeOffset(2025, 3, 31, 23, 0, 0, TimeSpan.Zero));
        Factory.Clock.Advance(TimeSpan.FromHours(2));
        var ids = await CreatePlayersAsync(client, "A1", "B1");

        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/games",
            new { blueTeam = new[] { ids[0] }, redTeam = new[] { ids[1] } }, Ct));

        Assert.Equal(new DateTime(2025, 4, 1, 1, 0, 0), created.GetProperty("playedAt").GetDateTime());
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
