using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// Un identifiant Riot n'est rattaché qu'à une game (ADR 0004) : le même rapport renvoyé (reprise
/// réseau, ou plusieurs joueurs équipés du Companion dans la même game) désigne la game déjà
/// rattachée, sans rien créer ni modifier, y compris sous envois simultanés.
/// </summary>
public class DedoublonnageRapportsTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const int Simultanes = 8;

    private static readonly string[] DixAutres =
        ["Annie", "Ashe", "Braum", "Fiora", "Galio", "Irelia", "Janna", "Karma", "Lulu", "Malphite"];

    private async Task<JsonElement> SerieAsync(HttpClient client, int seriesId) =>
        await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));

    private static Dictionary<int, string?> ChampionsJoues(JsonElement game) =>
        game.GetProperty("championsJoues").EnumerateArray()
            .ToDictionary(c => c.GetProperty("playerId").GetInt32(), c => c.GetProperty("championId").GetString());

    [Fact]
    public async Task Renvoyer_le_meme_rapport_designe_la_game_deja_rattachee_sans_rien_modifier()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        var premier = await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_8000000001", "blue", "A")));
        var avant = await SerieAsync(client, serie.SeriesId);
        Factory.Clock.Advance(TimeSpan.FromMinutes(3));

        var reponse = await EnvoyerRapportAsync(code, Rapport("EUW1_8000000001", "blue", "A"));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var renvoi = await ReadJsonAsync(reponse);
        Assert.Equal(premier.ToString(), renvoi.ToString());
        Assert.Equal(gameId, renvoi.GetProperty("gameId").GetInt32());
        Assert.Equal(avant.ToString(), (await SerieAsync(client, serie.SeriesId)).ToString());
    }

    [Fact]
    public async Task Le_rapport_d_un_autre_joueur_de_la_meme_partie_ne_touche_ni_sa_game_ni_la_suivante()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var game1 = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_8000000002", "blue", "A")));
        var game2 = await PlayGameAsync(client, serie.SeriesId, teamASide: "red");

        var reponse = await EnvoyerRapportAsync(code, Rapport("EUW1_8000000002", "red", "B", champions: DixAutres, emetteur: 4));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var renvoi = await ReadJsonAsync(reponse);
        Assert.Equal(game1, renvoi.GetProperty("gameId").GetInt32());
        Assert.Equal("A", renvoi.GetProperty("winner").GetString());
        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal(1, lue.GetProperty("teamAWins").GetInt32());
        Assert.Equal(0, lue.GetProperty("teamBWins").GetInt32());
        var games = lue.GetProperty("games");
        Assert.Equal(2, games.GetArrayLength());
        Assert.Equal("Garen", ChampionsJoues(games[0])[serie.TeamA[0]]);
        Assert.Equal(game2, games[1].GetProperty("id").GetInt32());
        Assert.Equal("pending", games[1].GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, games[1].GetProperty("riotMatchId").ValueKind);
    }

    [Fact]
    public async Task Des_envois_simultanes_du_meme_rapport_ne_le_rattachent_qu_une_fois()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        var reponses = await Task.WhenAll(Enumerable.Range(0, Simultanes).Select(i =>
            EnvoyerRapportAsync(code, Rapport("EUW1_8000000003", "blue", "B", emetteur: i))));

        Assert.All(reponses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        foreach (var reponse in reponses)
            Assert.Equal(gameId, (await ReadJsonAsync(reponse)).GetProperty("gameId").GetInt32());
        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal(0, lue.GetProperty("teamAWins").GetInt32());
        Assert.Equal(1, lue.GetProperty("teamBWins").GetInt32());
        var game = Assert.Single(lue.GetProperty("games").EnumerateArray());
        Assert.Equal("EUW1_8000000003", game.GetProperty("riotMatchId").GetString());
    }

    [Fact]
    public async Task Une_partie_riot_rattachee_a_une_serie_est_refusee_pour_une_autre_meme_sous_envois_simultanes()
    {
        var client = Factory.CreateSignedInClient();
        var serie1 = await CreateSerieAsync(client, format: 3);
        var game1 = await PlayGameAsync(client, serie1.SeriesId, teamASide: "blue");
        var code1 = await CodeCompanionAsync(client, serie1.SeriesId);
        var serie2Id = (await ReadJsonAsync(await client.PostAsJsonAsync("/series",
            new { teamA = serie1.TeamA, teamB = serie1.TeamB, format = 3, fearless = false }, Ct)))
            .GetProperty("seriesId").GetInt32();
        var game2 = await PlayGameAsync(client, serie2Id, teamASide: "blue");
        var code2 = await CodeCompanionAsync(client, serie2Id);

        var reponses = await Task.WhenAll(Enumerable.Range(0, Simultanes).Select(i =>
            EnvoyerRapportAsync(i % 2 == 0 ? code1 : code2, Rapport("EUW1_8000000004", "blue", "A", emetteur: i))));

        Assert.Equal(Simultanes / 2, reponses.Count(r => r.StatusCode == HttpStatusCode.OK));
        Assert.All(reponses.Where(r => r.StatusCode != HttpStatusCode.OK), r =>
        {
            Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
            Assert.Equal("application/problem+json", r.Content.Headers.ContentType?.MediaType);
        });
        var rattachees = new[] { (serie1.SeriesId, game1), (serie2Id, game2) }
            .Select(async s => (await SerieAsync(client, s.Item1)).GetProperty("games")[0])
            .Select(t => t.Result)
            .Where(g => g.GetProperty("riotMatchId").ValueKind != JsonValueKind.Null)
            .ToList();
        var rattachee = Assert.Single(rattachees);
        Assert.Equal("completed", rattachee.GetProperty("status").GetString());
    }

    [Fact]
    public async Task La_base_refuse_deux_games_rattachees_a_la_meme_partie_riot()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var game1 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        var game2 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await using var db = Factory.CreateDbContext();
        await db.Database.ExecuteSqlAsync(
            $"UPDATE custom_games SET riot_match_id = 'EUW1_8000000005' WHERE id = {game1}", Ct);

        var refus = await Assert.ThrowsAsync<PostgresException>(() => db.Database.ExecuteSqlAsync(
            $"UPDATE custom_games SET riot_match_id = 'EUW1_8000000005' WHERE id = {game2}", Ct));

        Assert.Equal(PostgresErrorCodes.UniqueViolation, refus.SqlState);
    }
}
