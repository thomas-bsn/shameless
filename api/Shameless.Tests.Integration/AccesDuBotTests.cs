using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// L'identité machine du bot Discord vers l'API (ADR 0013) : une clé distincte, en
/// <c>Authorization: Bearer</c>, qui n'ouvre que les routes <c>/bot/…</c>. Elle n'ouvre aucune route
/// des Utilisateurs, et une session n'ouvre aucune route du bot.
/// </summary>
public class AccesDuBotTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Le_bot_lit_une_game_avec_les_ids_discord_de_ses_joueurs_lies()
    {
        var organisateur = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(organisateur, format: 3);
        await LierADiscordAsync(serie.TeamA[0], "111111111111111111");
        await LierADiscordAsync(serie.TeamB[4], "222222222222222222");
        var gameId = await PlayGameAsync(organisateur, serie.SeriesId, teamASide: "red");

        var game = await ReadJsonAsync(await Factory.CreateBotClient().GetAsync($"/bot/games/{gameId}", Ct));

        Assert.Equal(gameId, game.GetProperty("gameId").GetInt32());
        Assert.Equal(serie.SeriesId, game.GetProperty("seriesId").GetInt32());
        Assert.Equal(1, game.GetProperty("gameNumber").GetInt32());
        Assert.Equal("pending", game.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, game.GetProperty("winner").ValueKind);
        var teamA = game.GetProperty("teamA").EnumerateArray().ToList();
        Assert.Equal(5, teamA.Count);
        Assert.Equal("A1", teamA[0].GetProperty("prenom").GetString());
        Assert.Equal(serie.TeamA[0], teamA[0].GetProperty("playerId").GetInt32());
        Assert.Equal("111111111111111111", teamA[0].GetProperty("discordId").GetString());
        Assert.Equal(JsonValueKind.Null, teamA[1].GetProperty("discordId").ValueKind);
        Assert.Equal("222222222222222222", game.GetProperty("teamB")[4].GetProperty("discordId").GetString());
    }

    [Fact]
    public async Task Une_game_inconnue_repond_404_au_bot()
    {
        await AssertRefusAsync(await Factory.CreateBotClient().GetAsync("/bot/games/999", Ct), HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("mauvaise-cle")]
    public async Task Sans_la_bonne_cle_du_bot_une_route_du_bot_repond_401(string? cle)
    {
        var client = Factory.CreateAnonymousClient();
        if (cle != null) client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", cle);

        await AssertRefusAsync(await client.GetAsync("/bot/games/1", Ct), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Une_session_d_utilisateur_n_ouvre_pas_les_routes_du_bot()
    {
        var client = Factory.CreateSignedInClient();

        await AssertRefusAsync(await client.GetAsync("/bot/games/1", Ct), HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task La_cle_du_bot_n_ouvre_pas_les_routes_des_utilisateurs()
    {
        var bot = Factory.CreateBotClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await bot.GetAsync("/players", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await bot.GetAsync("/games/1", Ct)).StatusCode);
    }
}
