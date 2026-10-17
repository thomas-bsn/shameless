using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Le Hall de la Honte : le cumul historique des titres de DOFD sur toutes les Games terminées, sans
/// remise à zéro mensuelle, les ex æquo au même rang, cohérent avec les votes de chaque Game.
/// </summary>
public class HallDeLaHonteTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const string DiscordA1 = "100000000000000001";
    private const string DiscordB1 = "100000000000000006";

    private Task<HttpResponseMessage> VoterDofdAsync(int gameId, string votant, int dofd) =>
        Factory.CreateBotClient().PutAsJsonAsync(
            $"/bot/games/{gameId}/votes",
            new { votantDiscordId = votant, mvpPlayerId = (int?)null, dofdPlayerId = dofd },
            Ct);

    private static List<(int Rang, int PlayerId, int Dofd)> Lignes(JsonElement hall) =>
        hall.GetProperty("joueurs").EnumerateArray()
            .Select(l => (l.GetProperty("rang").GetInt32(), l.GetProperty("playerId").GetInt32(), l.GetProperty("dofd").GetInt32()))
            .ToList();

    [Fact]
    public async Task Le_hall_cumule_les_dofd_de_toutes_les_games_sans_remise_a_zero_mensuelle()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5);
        await LierADiscordAsync(serie.TeamA[0], DiscordA1);
        await LierADiscordAsync(serie.TeamB[0], DiscordB1);
        int b2 = serie.TeamB[1], a3 = serie.TeamA[2];
        Factory.Clock.SetUtcNow(new DateTimeOffset(2026, 8, 14, 20, 0, 0, TimeSpan.Zero)); // connexions faites avant

        var aout = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await ReadJsonAsync(await VoterDofdAsync(aout, DiscordA1, b2));
        Factory.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 2, 20, 0, 0, TimeSpan.Zero));
        var octobre1 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await ReadJsonAsync(await VoterDofdAsync(octobre1, DiscordA1, b2));
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        var octobre2 = await PlayAndWinAsync(client, serie.SeriesId, "B");
        await ReadJsonAsync(await VoterDofdAsync(octobre2, DiscordB1, a3));
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        var hall = await ReadJsonAsync(await client.GetAsync("/hall-de-la-honte", Ct));

        Assert.Equal([(1, b2, 2), (2, a3, 1)], Lignes(hall));
        var premier = hall.GetProperty("joueurs")[0];
        Assert.Equal("B2", premier.GetProperty("prenom").GetString());
        Assert.Equal("B2#EUW", premier.GetProperty("riotId").GetString());
    }

    [Fact]
    public async Task Les_ex_aequo_partagent_le_rang_y_compris_a_egalite_dans_une_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await LierADiscordAsync(serie.TeamA[0], DiscordA1);
        await LierADiscordAsync(serie.TeamB[0], DiscordB1);
        int a4 = serie.TeamA[3], b3 = serie.TeamB[2], b5 = serie.TeamB[4];

        var game1 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await ReadJsonAsync(await VoterDofdAsync(game1, DiscordA1, b3));
        await ReadJsonAsync(await VoterDofdAsync(game1, DiscordB1, a4)); // égalité : deux DOFD
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        var game2 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await ReadJsonAsync(await VoterDofdAsync(game2, DiscordA1, b5));
        Factory.Clock.Advance(TimeSpan.FromMinutes(10));

        var hall = await ReadJsonAsync(await client.GetAsync("/hall-de-la-honte", Ct));

        Assert.Equal([1, 1, 1], Lignes(hall).Select(l => l.Rang));
        Assert.Equal([a4, b3, b5], Lignes(hall).Select(l => l.PlayerId).Order());
        Assert.All(Lignes(hall), l => Assert.Equal(1, l.Dofd));
    }

    [Fact]
    public async Task Le_hall_reprend_les_dofd_des_votes_clos_seulement()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await LierADiscordAsync(serie.TeamA[0], DiscordA1);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await ReadJsonAsync(await VoterDofdAsync(gameId, DiscordA1, serie.TeamB[1]));

        var pendantLesVotes = await ReadJsonAsync(await client.GetAsync("/hall-de-la-honte", Ct));
        Assert.Empty(Lignes(pendantLesVotes));

        Factory.Clock.Advance(TimeSpan.FromMinutes(10));
        var votes = await ReadJsonAsync(await client.GetAsync($"/games/{gameId}/votes", Ct));
        var hall = await ReadJsonAsync(await client.GetAsync("/hall-de-la-honte", Ct));

        var gagnants = votes.GetProperty("dofd").GetProperty("gagnants").EnumerateArray().Select(g => g.GetInt32());
        Assert.Equal(gagnants, Lignes(hall).Select(l => l.PlayerId));
    }
}
