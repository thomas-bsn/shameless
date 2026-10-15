using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class SeriesTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task A_best_of_3_tracks_team_wins_across_side_swaps_until_it_is_decided()
    {
        var client = Factory.CreateSignedInClient();
        var ids = await CreatePlayersAsync(client, "TeamA", "TeamB");
        int[] teamA = [ids[0]], teamB = [ids[1]];

        var first = await ReadJsonAsync(await client.PostAsJsonAsync("/games",
            new { blueTeam = teamA, redTeam = teamB, boFormat = 3 }, Ct));
        var seriesId = first.GetProperty("seriesId").GetInt32();

        var series = await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));
        Assert.Equal(3, series.GetProperty("format").GetInt32());
        Assert.Equal(["TeamA#EUW"], series.GetProperty("teamAReference").EnumerateArray().Select(r => r.GetString()));

        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{first.GetProperty("gameId").GetInt32()}/result", new { winner = "blue" }, Ct));

        var active = await ReadJsonAsync(await client.GetAsync("/series/active", Ct));
        var activeSeries = Assert.Single(active.GetProperty("series").EnumerateArray());
        Assert.Equal(seriesId, activeSeries.GetProperty("seriesId").GetInt32());
        Assert.Equal(1, activeSeries.GetProperty("teamAWins").GetInt32());

        // Game 2 : la team A passe côté rouge et gagne
        var second = await ReadJsonAsync(await client.PostAsJsonAsync("/games",
            new { blueTeam = teamB, redTeam = teamA, seriesId }, Ct));
        Assert.Equal(seriesId, second.GetProperty("seriesId").GetInt32());
        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{second.GetProperty("gameId").GetInt32()}/result", new { winner = "red" }, Ct));

        series = await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));
        Assert.Equal(2, series.GetProperty("teamAWins").GetInt32());
        Assert.Equal(0, series.GetProperty("teamBWins").GetInt32());
        Assert.Equal(1, series.GetProperty("blueWins").GetInt32());
        Assert.Equal(1, series.GetProperty("redWins").GetInt32());

        active = await ReadJsonAsync(await client.GetAsync("/series/active", Ct));
        Assert.Empty(active.GetProperty("series").EnumerateArray());
    }

    [Fact]
    public async Task Abandoning_a_series_deletes_it_with_its_games()
    {
        var client = Factory.CreateSignedInClient();
        var ids = await CreatePlayersAsync(client, "X", "Y");
        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/games",
            new { blueTeam = new[] { ids[0] }, redTeam = new[] { ids[1] }, boFormat = 5 }, Ct));
        var seriesId = created.GetProperty("seriesId").GetInt32();

        var abandon = await ReadJsonAsync(await client.PutAsync($"/series/{seriesId}/abandon", null, Ct));
        Assert.True(abandon.GetProperty("success").GetBoolean());

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/series/{seriesId}", Ct)).StatusCode);
        Assert.Empty((await ReadJsonAsync(await client.GetAsync("/games/pending", Ct))).EnumerateArray());
    }

    [Fact]
    public async Task Abandoning_an_unknown_series_returns_404()
    {
        var client = Factory.CreateSignedInClient();

        var response = await client.PutAsync("/series/999/abandon", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
