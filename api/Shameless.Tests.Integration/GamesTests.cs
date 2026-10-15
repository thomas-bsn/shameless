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
        var client = Factory.CreateAdminClient();
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
            .Select(p => (p.GetProperty("prenom").GetString(), p.GetProperty("team").GetString(), p.GetProperty("equipe").GetString()))
            .OrderBy(p => p.Item1);
        Assert.Equal([("A1", "blue", "A"), ("A2", "blue", "A"), ("B1", "red", "B"), ("B2", "red", "B")], participants);

        var result = await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct));
        Assert.Equal("A", result.GetProperty("winner").GetString());
        Assert.Equal("completed", result.GetProperty("status").GetString());

        pending = await ReadJsonAsync(await client.GetAsync("/games/pending", Ct));
        Assert.Empty(pending.EnumerateArray());
    }

    [Fact]
    public async Task Completed_games_appear_in_the_monthly_history_with_recalculated_costs()
    {
        var client = Factory.CreateAdminClient();
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

        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "B" }, Ct));

        history = await ReadJsonAsync(await client.GetAsync($"/games/history/{now.Year}/{now.Month}", Ct));
        var group = Assert.Single(history.GetProperty("games").EnumerateArray());
        Assert.Equal(0, group.GetProperty("seriesId").GetInt32());
        Assert.Equal(1, group.GetProperty("format").GetInt32());
        var game = Assert.Single(group.GetProperty("games").EnumerateArray());
        Assert.Equal("B", game.GetProperty("winner").GetString());
        Assert.Equal("blue", game.GetProperty("teamASide").GetString());
        Assert.Equal("red", game.GetProperty("teamBSide").GetString());

        // DIAMOND IV = 2300 MMR → cost 4 ; sans peak = 0 MMR → cost 1 (moins de 3 games : pas d'ajustement)
        var blue = Assert.Single(game.GetProperty("teamA").EnumerateArray());
        Assert.Equal("Blue", blue.GetProperty("prenom").GetString());
        Assert.Equal(4m, blue.GetProperty("cost").GetDecimal());
        var red = Assert.Single(game.GetProperty("teamB").EnumerateArray());
        Assert.Equal(1m, red.GetProperty("cost").GetDecimal());
    }

    [Fact]
    public async Task Le_cost_compte_les_victoires_de_l_equipe_quel_que_soit_son_side()
    {
        var client = Factory.CreateAdminClient();
        var ids = await CreatePlayersAsync(client, "A1", "B1");
        int[] equipeA = [ids[0]], equipeB = [ids[1]];

        // L'Équipe A gagne trois games en changeant de Side à chaque fois
        int? seriesId = null;
        foreach (var aOnBlue in new[] { true, false, true })
        {
            var game = await ReadJsonAsync(await client.PostAsJsonAsync("/games", new
            {
                blueTeam = aOnBlue ? equipeA : equipeB,
                redTeam = aOnBlue ? equipeB : equipeA,
                seriesId,
                boFormat = seriesId == null ? 5 : (int?)null
            }, Ct));
            seriesId = game.GetProperty("seriesId").GetInt32();
            await ReadJsonAsync(await client.PutAsJsonAsync(
                $"/games/{game.GetProperty("gameId").GetInt32()}/result", new { winner = "A" }, Ct));
        }

        // 3 games : confiance 3/20, ajustement (winrate - 50 %) × 150 × 0,15 = ±11
        var a = await ReadJsonAsync(await client.GetAsync($"/players/{ids[0]}/cost", Ct));
        var b = await ReadJsonAsync(await client.GetAsync($"/players/{ids[1]}/cost", Ct));
        Assert.Equal(11, a.GetProperty("winrateAdjustment").GetDecimal());
        Assert.Equal(-11, b.GetProperty("winrateAdjustment").GetDecimal());
    }

    [Fact]
    public async Task Entering_the_result_of_an_unknown_game_returns_404()
    {
        var client = Factory.CreateAdminClient();

        var response = await client.PutAsJsonAsync("/games/999/result", new { winner = "A" }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
