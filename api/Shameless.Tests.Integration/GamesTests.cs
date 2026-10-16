using System.Net;
using System.Net.Http.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class GamesTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task Une_game_est_en_attente_jusqu_a_la_saisie_de_son_resultat()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);

        var created = await client.PostAsJsonAsync($"/series/{serie.SeriesId}/games", new { teamASide = "red" }, Ct);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var gameId = (await ReadJsonAsync(created)).GetProperty("id").GetInt32();

        var pending = await ReadJsonAsync(await client.GetAsync("/games/pending", Ct));
        var game = Assert.Single(pending.EnumerateArray());
        Assert.Equal(gameId, game.GetProperty("id").GetInt32());
        Assert.Equal(serie.SeriesId, game.GetProperty("seriesId").GetInt32());
        Assert.Equal("pending", game.GetProperty("status").GetString());
        var participants = game.GetProperty("participants").EnumerateArray()
            .Select(p => (p.GetProperty("prenom").GetString(), p.GetProperty("side").GetString(), p.GetProperty("equipe").GetString()))
            .OrderBy(p => p.Item1)
            .ToList();
        Assert.Equal(10, participants.Count);
        Assert.Equal(("A1", "red", "A"), participants[0]);
        Assert.Equal(("B1", "blue", "B"), participants[5]);

        var result = await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct));
        Assert.Equal("A", result.GetProperty("winner").GetString());
        Assert.Equal("completed", result.GetProperty("status").GetString());

        pending = await ReadJsonAsync(await client.GetAsync("/games/pending", Ct));
        Assert.Empty(pending.EnumerateArray());
    }

    [Fact]
    public async Task Les_games_terminees_apparaissent_dans_l_historique_du_mois_avec_le_cost_recalcule()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/link", new { playerId = serie.TeamA[0] }, Ct));
        await ReadJsonAsync(await client.PostAsJsonAsync("/players/peak",
            new { peakTier = "DIAMOND", peakDivision = 4, peakSeason = "S2024", peakLp = 0 }, Ct));
        var gameId = await PlayGameAsync(client, serie.SeriesId);

        var now = Factory.Clock.GetUtcNow();
        var history = await ReadJsonAsync(await client.GetAsync($"/games/history/{now.Year}/{now.Month}", Ct));
        Assert.Empty(history.GetProperty("games").EnumerateArray());

        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "B" }, Ct));

        history = await ReadJsonAsync(await client.GetAsync($"/games/history/{now.Year}/{now.Month}", Ct));
        var group = Assert.Single(history.GetProperty("games").EnumerateArray());
        Assert.Equal(serie.SeriesId, group.GetProperty("seriesId").GetInt32());
        Assert.Equal(1, group.GetProperty("format").GetInt32());
        var game = Assert.Single(group.GetProperty("games").EnumerateArray());
        Assert.Equal("B", game.GetProperty("winner").GetString());
        Assert.Equal("blue", game.GetProperty("teamASide").GetString());
        Assert.Equal("red", game.GetProperty("teamBSide").GetString());

        // Moins de cinq games : chacun garde son cost de départ, celui de son Rang SoloQ GOLD II (le peak ne compte pas).
        var a1 = game.GetProperty("teamA").EnumerateArray().Single(p => p.GetProperty("prenom").GetString() == "A1");
        Assert.Equal(2m, a1.GetProperty("cost").GetDecimal());
        var b1 = game.GetProperty("teamB").EnumerateArray().Single(p => p.GetProperty("prenom").GetString() == "B1");
        Assert.Equal(2m, b1.GetProperty("cost").GetDecimal());
    }

    [Fact]
    public async Task L_historique_donne_l_issue_de_chaque_serie_et_l_equipe_gagnante()
    {
        var client = Factory.CreateSignedInClient();
        var gagnee = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, gagnee.SeriesId, "B");
        var interrompue = await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{gagnee.SeriesId}/rematch",
            new { format = 3, fearless = false, alternateSides = true }, Ct));
        var interrompueId = interrompue.GetProperty("seriesId").GetInt32();
        await PlayAndWinAsync(client, interrompueId, "A");
        await ReadJsonAsync(await client.PostAsync($"/series/{interrompueId}/stop", null, Ct));

        var now = Factory.Clock.GetUtcNow();
        var history = await ReadJsonAsync(await client.GetAsync($"/games/history/{now.Year}/{now.Month}", Ct));

        var issues = history.GetProperty("games").EnumerateArray()
            .ToDictionary(s => s.GetProperty("seriesId").GetInt32(),
                s => (s.GetProperty("outcome").GetString(), s.GetProperty("winningTeam").GetString()));
        Assert.Equal(("Gagnee", "B"), issues[gagnee.SeriesId]);
        Assert.Equal(("Interrompue", null), issues[interrompueId]);
    }

    [Fact]
    public async Task Une_game_lancee_mais_pas_terminee_ne_compte_pas_dans_les_games_du_mois()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayGameAsync(client, serie.SeriesId);

        var cost = await ReadJsonAsync(await client.GetAsync($"/players/{serie.TeamA[0]}/cost", Ct));
        Assert.Equal(2, cost.GetProperty("gamesTerminees").GetInt32());
    }

    [Fact]
    public async Task Le_cost_est_recalcule_apres_chaque_game_et_compte_les_victoires_de_l_equipe_quel_que_soit_son_side()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5);

        // Les Équipes changent de Side à chaque game : A gagne en bleu, B en rouge.
        var suivi = new List<(int, decimal)>();
        foreach (var (gagnante, side) in new[] { ("A", "blue"), ("B", "red"), ("A", "blue"), ("B", "red"), ("A", "blue") })
        {
            await PlayAndWinAsync(client, serie.SeriesId, gagnante, side);
            var a = await ReadJsonAsync(await client.GetAsync($"/players/{serie.TeamA[0]}/cost", Ct));
            suivi.Add((a.GetProperty("victoires").GetInt32(), a.GetProperty("cost").GetDecimal()));
        }

        // Le cost ne bouge qu'à la cinquième game : 3 victoires sur 5 → 2,1 ; 2 sur 5 → 1,9.
        Assert.Equal([(1, 2m), (1, 2m), (2, 2m), (2, 2m), (3, 2.1m)], suivi);
        var b = await ReadJsonAsync(await client.GetAsync($"/players/{serie.TeamB[0]}/cost", Ct));
        Assert.Equal((2, 1.9m), (b.GetProperty("victoires").GetInt32(), b.GetProperty("cost").GetDecimal()));
    }

    [Fact]
    public async Task Saisir_le_resultat_d_une_game_inconnue_repond_404()
    {
        var client = Factory.CreateSignedInClient();

        var response = await client.PutAsJsonAsync("/games/999/result", new { winner = "A" }, Ct);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task La_creation_de_game_avec_des_equipes_libres_hors_serie_est_retiree()
    {
        var client = Factory.CreateSignedInClient();
        var ids = await CreatePlayersAsync(client, "A1", "B1");

        var response = await client.PostAsJsonAsync("/games",
            new { blueTeam = new[] { ids[0] }, redTeam = new[] { ids[1] }, boFormat = 3 }, Ct);

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed,
            $"POST /games returned {(int)response.StatusCode}");
    }
}
