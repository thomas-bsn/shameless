using System.Net;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// La page d'une Game (<c>GET /games/{id}</c>) : sa place dans la Série, ses deux Équipes, le
/// vainqueur et les stats envoyées par le Companion. C'est le lien de récap posté par le bot.
/// </summary>
public class RecapDeGameTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task La_page_d_une_game_terminee_donne_sa_serie_ses_equipes_son_vainqueur_et_ses_stats()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "red");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_7000000001", teamASide: "red", gagnante: "B")));

        var game = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));

        Assert.Equal(gameId, game.GetProperty("id").GetInt32());
        Assert.Equal(serie.SeriesId, game.GetProperty("seriesId").GetInt32());
        Assert.Equal(2, game.GetProperty("gameNumber").GetInt32());
        Assert.Equal(3, game.GetProperty("bestOf").GetInt32());
        Assert.Equal("completed", game.GetProperty("status").GetString());
        Assert.Equal("B", game.GetProperty("winner").GetString());
        Assert.Equal("red", game.GetProperty("teamASide").GetString());
        Assert.Equal(1, game.GetProperty("teamAWins").GetInt32());
        Assert.Equal(1, game.GetProperty("teamBWins").GetInt32());
        Assert.Equal(1834, game.GetProperty("match").GetProperty("durationSeconds").GetInt32());
        var teamA = game.GetProperty("teamA").EnumerateArray().ToList();
        Assert.Equal(["A1", "A2", "A3", "A4", "A5"], teamA.Select(p => p.GetProperty("prenom").GetString()));
        Assert.Equal("Garen", teamA[0].GetProperty("championId").GetString());
        Assert.Equal(2, teamA[0].GetProperty("stats").GetProperty("deaths").GetInt32());
        Assert.Equal(5, game.GetProperty("teamB").GetArrayLength());
    }

    [Fact]
    public async Task La_page_d_une_game_en_attente_n_a_pas_encore_de_vainqueur()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayGameAsync(client, serie.SeriesId);

        var game = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));

        Assert.Equal("pending", game.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, game.GetProperty("winner").ValueKind);
        Assert.Equal(JsonValueKind.Null, game.GetProperty("completedAt").ValueKind);
        Assert.Equal(1, game.GetProperty("gameNumber").GetInt32());
    }

    [Fact]
    public async Task Une_game_inconnue_repond_404()
    {
        var client = Factory.CreateSignedInClient();

        await AssertRefusAsync(await client.GetAsync("/games/999", Ct), HttpStatusCode.NotFound);
    }
}
