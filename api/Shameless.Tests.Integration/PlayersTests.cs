using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class PlayersTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task A_created_player_appears_in_the_player_list_with_their_riot_rank()
    {
        Factory.Riot.AddAccount("Faker#KR1", "CHALLENGER", "I", lp: 1200);
        var client = Factory.CreateSignedInClient();

        var created = await client.PostAsJsonAsync("/players", new { prenom = "Lee", riotId = "Faker#KR1" }, Ct);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);

        var players = await client.GetFromJsonAsync<JsonElement>("/players", Ct);
        var player = Assert.Single(players.EnumerateArray());
        Assert.Equal("Lee", player.GetProperty("prenom").GetString());
        Assert.Equal("Faker#KR1", player.GetProperty("riotId").GetString());
        Assert.Equal("CHALLENGER", player.GetProperty("rankTier").GetString());
        Assert.Equal(JsonValueKind.Null, player.GetProperty("rankDivision").ValueKind);
        Assert.Equal(1200, player.GetProperty("lp").GetInt32());
    }

    [Fact]
    public async Task A_signed_in_user_can_link_their_discord_account_to_a_player()
    {
        var client = Factory.CreateSignedInClient(discordId: "2001");
        var playerId = await CreatePlayerAsync(client, "Alice");

        var notLinked = await client.GetAsync("/players/me", Ct);
        Assert.Equal(HttpStatusCode.NotFound, notLinked.StatusCode);

        var link = await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId }, Ct));
        Assert.True(link.GetProperty("success").GetBoolean());

        var me = await ReadJsonAsync(await client.GetAsync("/players/me", Ct));
        Assert.Equal(playerId, me.GetProperty("id").GetInt32());
        Assert.Equal("Alice", me.GetProperty("prenom").GetString());
    }

    [Fact]
    public async Task A_linked_player_can_set_and_delete_their_own_peak()
    {
        var client = Factory.CreateSignedInClient(discordId: "2002");
        var playerId = await CreatePlayerAsync(client, "Bob");
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId }, Ct));

        await ReadJsonAsync(await client.PostAsJsonAsync("/players/peak",
            new { peakTier = "DIAMOND", peakDivision = 2, peakSeason = "S2024", peakLp = 40 }, Ct));

        var me = await ReadJsonAsync(await client.GetAsync("/players/me", Ct));
        Assert.Equal("DIAMOND", me.GetProperty("peakTier").GetString());
        Assert.Equal(2, me.GetProperty("peakDivision").GetInt32());
        Assert.Equal("S2024", me.GetProperty("peakSeason").GetString());
        Assert.Equal(40, me.GetProperty("peakLp").GetInt32());

        await ReadJsonAsync(await client.DeleteAsync("/players/peak", Ct));

        me = await ReadJsonAsync(await client.GetAsync("/players/me", Ct));
        Assert.Equal(JsonValueKind.Null, me.GetProperty("peakTier").ValueKind);
        Assert.Equal(0, me.GetProperty("peakLp").GetInt32());
    }

    [Fact]
    public async Task The_cost_preview_is_computed_from_the_peak_rank()
    {
        var client = Factory.CreateSignedInClient(discordId: "2003");
        var playerId = await CreatePlayerAsync(client, "Chloe");
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId }, Ct));
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/peak",
            new { peakTier = "GOLD", peakDivision = 2, peakSeason = "S2024", peakLp = 0 }, Ct));

        var cost = await ReadJsonAsync(await client.GetAsync($"/players/{playerId}/cost", Ct));

        Assert.Equal("Chloe", cost.GetProperty("name").GetString());
        Assert.Equal(1500, cost.GetProperty("baseMMR").GetInt32());
        Assert.Equal(1500, cost.GetProperty("mmr").GetInt32());
        Assert.Equal(2m, cost.GetProperty("cost").GetDecimal());
        Assert.Equal("C", cost.GetProperty("tier").GetString());
    }
}
