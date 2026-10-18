using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// Le rapport de fin de game envoyé par le Companion avec le code de sa Série : s'il correspond à la
/// composition de la game en attente, il la termine comme une saisie manuelle et y laisse les données
/// Riot et les champions joués.
/// </summary>
public class RapportsCompanionTests(ApiFactory factory) : IntegrationTest(factory)
{
    private async Task<JsonElement> SerieAsync(HttpClient client, int seriesId) =>
        await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));

    [Fact]
    public async Task Un_rapport_conforme_termine_la_game_en_attente_avec_le_bon_vainqueur()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "red");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        var reponse = await EnvoyerRapportAsync(code, Rapport("EUW1_7000000001", teamASide: "red", gagnante: "B"));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var recu = await ReadJsonAsync(reponse);
        Assert.Equal(gameId, recu.GetProperty("gameId").GetInt32());
        Assert.Equal(serie.SeriesId, recu.GetProperty("seriesId").GetInt32());
        Assert.Equal("B", recu.GetProperty("winner").GetString());

        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal(0, lue.GetProperty("teamAWins").GetInt32());
        Assert.Equal(1, lue.GetProperty("teamBWins").GetInt32());
        Assert.Equal("EnCours", lue.GetProperty("outcome").GetString());
        var game = lue.GetProperty("games")[0];
        Assert.Equal("completed", game.GetProperty("status").GetString());
        Assert.Equal("B", game.GetProperty("winner").GetString());
    }

    [Fact]
    public async Task Chaque_participant_garde_son_champion_joue_et_ses_donnees_riot_comme_la_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_7000000002", teamASide: "blue", gagnante: "A")));

        var game = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[0];
        Assert.Equal("EUW1_7000000002", game.GetProperty("riotMatchId").GetString());
        var champions = game.GetProperty("championsJoues").EnumerateArray()
            .ToDictionary(c => c.GetProperty("playerId").GetInt32(), c => c.GetProperty("championId").GetString());
        Assert.Equal(10, champions.Count);
        Assert.Equal("Garen", champions[serie.TeamA[0]]);
        Assert.Equal("Thresh", champions[serie.TeamA[4]]);
        Assert.Equal("Darius", champions[serie.TeamB[0]]);
        Assert.Equal("Nami", champions[serie.TeamB[4]]);

        // Les données Riot brutes sont gardées telles quelles (l'historique n'en expose que la lecture typée) : relues en base.
        await using var db = Factory.CreateDbContext();
        var donnees = await db.Database.SqlQuery<string>(
            $"SELECT riot_match_data::text AS \"Value\" FROM custom_games WHERE id = {gameId}").SingleAsync(Ct);
        Assert.Equal(1834, JsonDocument.Parse(donnees).RootElement.GetProperty("gameDuration").GetInt32());
        var stats = await db.Database.SqlQuery<string>(
            $"SELECT p.riot_participant_data::text AS \"Value\" FROM custom_game_participants p WHERE p.custom_game_id = {gameId} AND p.player_id = {serie.TeamB[0]}").SingleAsync(Ct);
        Assert.Equal(12005, JsonDocument.Parse(stats).RootElement.GetProperty("totalDamageDealtToChampions").GetInt32());
    }

    [Fact]
    public async Task Un_rapport_conforme_dont_le_side_differe_corrige_le_side_de_la_game_et_l_alternance_repart_de_lui()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");

        // Le pile ou face prévoyait A en blue ; le Companion a vu A en red, et gagnant.
        var recu = await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_7000000060", teamASide: "red", gagnante: "A")));

        Assert.Equal("Accepte", recu.GetProperty("statut").GetString());
        var game = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[0];
        Assert.Equal(gameId, game.GetProperty("id").GetInt32());
        Assert.Equal(("red", "blue", "A"), (game.GetProperty("teamASide").GetString(), game.GetProperty("teamBSide").GetString(), game.GetProperty("winner").GetString()));
        var suivante = await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/games", null, Ct));
        Assert.Equal("blue", suivante.GetProperty("teamASide").GetString());
    }

    [Fact]
    public async Task Les_rapports_successifs_d_un_bo_terminent_des_games_distinctes_jusqu_a_l_issue_de_la_serie()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        var game1 = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var recu1 = await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_7000000010", teamASide: "blue", gagnante: "A")));
        var game2 = await PlayGameAsync(client, serie.SeriesId, teamASide: null);
        var recu2 = await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_7000000011", teamASide: "red", gagnante: "A")));

        Assert.Equal(game1, recu1.GetProperty("gameId").GetInt32());
        Assert.Equal(game2, recu2.GetProperty("gameId").GetInt32());
        Assert.Equal(2, recu2.GetProperty("teamAWins").GetInt32());
        Assert.Equal("Gagnee", recu2.GetProperty("outcome").GetString());
        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal("Gagnee", lue.GetProperty("outcome").GetString());
        Assert.Equal("A", lue.GetProperty("winningTeam").GetString());
        Assert.Equal(["EUW1_7000000010", "EUW1_7000000011"],
            lue.GetProperty("games").EnumerateArray().Select(g => g.GetProperty("riotMatchId").GetString()));
    }

    [Fact]
    public async Task La_composition_comparee_est_celle_de_la_game_remplacements_compris()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_7000000020", teamASide: "blue", gagnante: "A")));
        var remplacant = await CreatePlayerAsync(client, "C1");
        await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{serie.SeriesId}/replacements",
            new { replacedPlayerId = serie.TeamA[4], replacementPlayerId = remplacant }, Ct));
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");

        var avecLeRemplace = await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_7000000021", teamASide: "blue", gagnante: "B")));
        var revue = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[1].GetProperty("rapportEnRevue");
        await ReadJsonAsync(await client.PostAsync($"/rapports/{revue.GetProperty("id").GetInt32()}/rejeter", null, Ct));
        var avecLeRemplacant = await EnvoyerRapportAsync(code, Rapport("EUW1_7000000022", teamASide: "blue", gagnante: "B",
            equipeA: ["A1", "A2", "A3", "A4", "C1"]));

        Assert.Equal("EnRevue", avecLeRemplace.GetProperty("statut").GetString());
        Assert.Equal(remplacant, Assert.Single(revue.GetProperty("absents").EnumerateArray()).GetProperty("playerId").GetInt32());
        Assert.Equal(HttpStatusCode.OK, avecLeRemplacant.StatusCode);
        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal(1, lue.GetProperty("teamBWins").GetInt32());
        var champions = lue.GetProperty("games")[1].GetProperty("championsJoues").EnumerateArray()
            .ToDictionary(c => c.GetProperty("playerId").GetInt32(), c => c.GetProperty("championId").GetString());
        Assert.Equal("Thresh", champions[remplacant]);
        Assert.DoesNotContain(serie.TeamA[4], champions.Keys);
    }

    [Fact]
    public async Task Un_rapport_dont_les_joueurs_ne_sont_pas_la_composition_de_la_game_ne_la_termine_pas()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await CreatePlayerAsync(client, "X1");
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        var joueurEtranger = await EnvoyerRapportAsync(code, Rapport("EUW1_7000000030", teamASide: "blue", gagnante: "A",
            equipeA: ["A1", "A2", "A3", "A4", "X1"]));

        Assert.Equal("EnRevue", (await ReadJsonAsync(joueurEtranger)).GetProperty("statut").GetString());
        var game = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[0];
        Assert.Equal("pending", game.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, game.GetProperty("riotMatchId").ValueKind);
    }

    [Fact]
    public async Task Un_rapport_sans_game_en_attente_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        var avantLaGame = await EnvoyerRapportAsync(code, Rapport("EUW1_7000000040", teamASide: "blue", gagnante: "A"));

        await AssertRefusAsync(avantLaGame, HttpStatusCode.Conflict);
        Assert.Empty((await SerieAsync(client, serie.SeriesId)).GetProperty("games").EnumerateArray());
    }

    [Fact]
    public async Task La_saisie_manuelle_reste_le_secours_et_un_rapport_arrive_apres_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await PlayAndWinAsync(client, serie.SeriesId, "A", "blue");

        var apresLaSaisie = await EnvoyerRapportAsync(code, Rapport("EUW1_7000000050", teamASide: "blue", gagnante: "B"));

        await AssertRefusAsync(apresLaSaisie, HttpStatusCode.Conflict);
        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal(1, lue.GetProperty("teamAWins").GetInt32());
        Assert.Equal(0, lue.GetProperty("teamBWins").GetInt32());
        Assert.Equal(JsonValueKind.Null, lue.GetProperty("games")[0].GetProperty("riotMatchId").ValueKind);
    }

    [Fact]
    public async Task Un_rapport_mal_forme_est_refuse_sans_toucher_a_la_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        var deuxVainqueurs = JsonSerializer.SerializeToNode(Rapport("EUW1_7000000060", "blue", "A"))!;
        deuxVainqueurs["joueurs"]![9]!["victoire"] = true;
        var neufJoueurs = JsonSerializer.SerializeToNode(Rapport("EUW1_7000000061", "blue", "A"))!;
        neufJoueurs["joueurs"]!.AsArray().RemoveAt(0);
        var sansIdentifiant = JsonSerializer.SerializeToNode(Rapport("", "blue", "A"))!;

        foreach (var rapport in new[] { deuxVainqueurs, neufJoueurs, sansIdentifiant })
            await AssertRefusAsync(await EnvoyerRapportAsync(code, rapport), HttpStatusCode.BadRequest);

        var game = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[0];
        Assert.Equal("pending", game.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Un_rapport_avec_un_code_inconnu_ou_renouvele_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var ancien = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/code-companion/renouveler", null, Ct));

        await AssertRefusAsync(await EnvoyerRapportAsync(ancien, Rapport("EUW1_7000000070", "blue", "A")), HttpStatusCode.NotFound);
        await AssertRefusAsync(await EnvoyerRapportAsync("inconnu", Rapport("EUW1_7000000070", "blue", "A")), HttpStatusCode.NotFound);
        Assert.Equal("pending", (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Une_game_sans_rapport_n_a_ni_identifiant_riot_ni_champion_joue()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        var game = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[0];
        Assert.Equal(JsonValueKind.Null, game.GetProperty("riotMatchId").ValueKind);
        Assert.Empty(game.GetProperty("championsJoues").EnumerateArray());
    }
}
