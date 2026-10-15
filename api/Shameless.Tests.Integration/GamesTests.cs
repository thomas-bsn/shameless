using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class GamesTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task A_recorded_game_is_pending_until_its_result_is_entered()
    {
        var client = Factory.CreateSignedInClient();
        var ids = await CreatePlayersAsync(client, "A1", "A2", "B1", "B2");

        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/games",
            new { blueTeam = ids[..2], redTeam = ids[2..] }, Ct));
        var gameId = created.GetProperty("gameId").GetInt32();
        Assert.Equal(JsonValueKind.Null, created.GetProperty("seriesId").ValueKind);
        Assert.Equal("Game enregistrée !", created.GetProperty("message").GetString());

        var pending = await ReadJsonAsync(await client.GetAsync("/games/pending", Ct));
        var game = Assert.Single(pending.EnumerateArray());
        Assert.Equal(gameId, game.GetProperty("id").GetInt32());
        Assert.Equal("pending", game.GetProperty("status").GetString());
        var participants = game.GetProperty("participants").EnumerateArray()
            .Select(p => (p.GetProperty("prenom").GetString(), p.GetProperty("team").GetString()))
            .OrderBy(p => p.Item1);
        Assert.Equal([("A1", "blue"), ("A2", "blue"), ("B1", "red"), ("B2", "red")], participants);

        var result = await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "blue" }, Ct));
        Assert.Equal("blue", result.GetProperty("winner").GetString());
        Assert.Equal("completed", result.GetProperty("status").GetString());

        pending = await ReadJsonAsync(await client.GetAsync("/games/pending", Ct));
        Assert.Empty(pending.EnumerateArray());
    }

    [Fact]
    public async Task Completed_games_appear_in_the_monthly_history_with_recalculated_costs()
    {
        var client = Factory.CreateSignedInClient(discordId: "3001");
        var ids = await CreatePlayersAsync(client, "Blue", "Red");
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId = ids[0] }, Ct));
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/peak",
            new { peakTier = "DIAMOND", peakDivision = 4, peakSeason = "S2024", peakLp = 0 }, Ct));

        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/games",
            new { blueTeam = new[] { ids[0] }, redTeam = new[] { ids[1] } }, Ct));
        var gameId = created.GetProperty("gameId").GetInt32();

        var now = DateTime.UtcNow;
        var history = await ReadJsonAsync(await client.GetAsync($"/games/history/{now.Year}/{now.Month}", Ct));
        Assert.Empty(history.GetProperty("games").EnumerateArray());

        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "red" }, Ct));

        history = await ReadJsonAsync(await client.GetAsync($"/games/history/{now.Year}/{now.Month}", Ct));
        var group = Assert.Single(history.GetProperty("games").EnumerateArray());
        Assert.Equal(0, group.GetProperty("seriesId").GetInt32());
        Assert.Equal(1, group.GetProperty("format").GetInt32());
        var game = Assert.Single(group.GetProperty("games").EnumerateArray());
        Assert.Equal("red", game.GetProperty("winner").GetString());

        // DIAMOND IV = 2300 MMR → cost 4 ; sans peak = 0 MMR → cost 1 (moins de 3 games : pas d'ajustement)
        var blue = Assert.Single(game.GetProperty("blueTeam").EnumerateArray());
        Assert.Equal("Blue", blue.GetProperty("prenom").GetString());
        Assert.Equal(4m, blue.GetProperty("cost").GetDecimal());
        var red = Assert.Single(game.GetProperty("redTeam").EnumerateArray());
        Assert.Equal(1m, red.GetProperty("cost").GetDecimal());
    }

    [Fact]
    public async Task Entering_the_result_of_an_unknown_game_returns_404()
    {
        var client = Factory.CreateSignedInClient();

        var response = await client.PutAsJsonAsync("/games/999/result", new { winner = "blue" }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
