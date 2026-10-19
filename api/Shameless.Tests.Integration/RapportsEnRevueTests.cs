using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// Un rapport du Companion dont les joueurs détectés ne sont pas la composition de la game en attente
/// est en revue : la game reste en attente jusqu'à ce que l'organisateur connecté l'accepte ou le
/// rejette.
/// </summary>
public class RapportsEnRevueTests(ApiFactory factory) : IntegrationTest(factory)
{
    private async Task<JsonElement> SerieAsync(HttpClient client, int seriesId) =>
        await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));

    private record EnRevue(CreatedSerie Serie, int GameId, string Code, int RapportId, int Etranger);

    /// <summary>
    /// Une Série BO3 avec une game en attente (Équipe A en blue), et un rapport de cette game où X1
    /// joue à la place de A5 : il est en revue.
    /// </summary>
    private async Task<EnRevue> MettreEnRevueAsync(HttpClient client, string gagnante = "A")
    {
        var serie = await CreateSerieAsync(client, format: 3);
        var etranger = await CreatePlayerAsync(client, "X1");
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_8000000001", teamASide: "blue", gagnante,
            equipeA: ["A1", "A2", "A3", "A4", "X1"])));
        var revue = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[0].GetProperty("rapportEnRevue");
        return new EnRevue(serie, gameId, code, revue.GetProperty("id").GetInt32(), etranger);
    }

    [Fact]
    public async Task Un_ecart_de_composition_met_le_rapport_en_revue_sans_terminer_la_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await CreatePlayerAsync(client, "X1");
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        var reponse = await EnvoyerRapportAsync(code, Rapport("EUW1_8000000002", teamASide: "blue", gagnante: "A",
            equipeA: ["A1", "A2", "A3", "A4", "X1"]));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var recu = await ReadJsonAsync(reponse);
        Assert.Equal("EnRevue", recu.GetProperty("statut").GetString());
        Assert.Equal(gameId, recu.GetProperty("gameId").GetInt32());
        Assert.Equal(JsonValueKind.Null, recu.GetProperty("winner").ValueKind);

        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal(0, lue.GetProperty("teamAWins").GetInt32());
        var game = lue.GetProperty("games")[0];
        Assert.Equal("pending", game.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, game.GetProperty("riotMatchId").ValueKind);
        var revue = game.GetProperty("rapportEnRevue");
        Assert.Equal("EUW1_8000000002", revue.GetProperty("riotMatchId").GetString());
        Assert.Equal("A", revue.GetProperty("equipeGagnante").GetString());
        var absent = Assert.Single(revue.GetProperty("absents").EnumerateArray());
        Assert.Equal(serie.TeamA[4], absent.GetProperty("playerId").GetInt32());
        var inattendu = Assert.Single(revue.GetProperty("inattendus").EnumerateArray());
        Assert.Equal("puuid-X1#EUW", inattendu.GetProperty("puuid").GetString());
        Assert.Equal("X1", inattendu.GetProperty("joueur").GetProperty("prenom").GetString());
        Assert.Equal("blue", inattendu.GetProperty("side").GetString());
        Assert.Equal("Thresh", inattendu.GetProperty("championId").GetString());
        Assert.Empty(revue.GetProperty("mauvaisSide").EnumerateArray());
    }

    [Fact]
    public async Task Un_joueur_inconnu_de_shameless_est_montre_par_son_puuid()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_8000000003", teamASide: "blue", gagnante: "B",
            equipeB: ["B1", "B2", "B3", "B4", "Inconnu"])));

        var revue = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[0].GetProperty("rapportEnRevue");
        var inattendu = Assert.Single(revue.GetProperty("inattendus").EnumerateArray());
        Assert.Equal("puuid-Inconnu#EUW", inattendu.GetProperty("puuid").GetString());
        Assert.Equal(JsonValueKind.Null, inattendu.GetProperty("joueur").ValueKind);
        Assert.Equal("B", revue.GetProperty("equipeGagnante").GetString());
    }

    [Fact]
    public async Task Un_joueur_inconnu_est_signale_avec_son_riotid()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Riot.AddAccount("Inconnu#EUW", "GOLD", "II");
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        var recu = await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_8000000005", teamASide: "blue", gagnante: "B",
            equipeB: ["B1", "B2", "B3", "B4", "Inconnu"])));

        Assert.Equal("EnRevue", recu.GetProperty("statut").GetString());
        var game = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[0];
        Assert.Equal(gameId, game.GetProperty("id").GetInt32());
        Assert.Equal("pending", game.GetProperty("status").GetString());
        var inattendu = Assert.Single(game.GetProperty("rapportEnRevue").GetProperty("inattendus").EnumerateArray());
        Assert.Equal("Inconnu#EUW", inattendu.GetProperty("riotId").GetString());
        Assert.Equal(JsonValueKind.Null, inattendu.GetProperty("joueur").ValueKind);
    }

    [Fact]
    public async Task Un_joueur_inconnu_dont_riot_ne_repond_pas_reste_signale_sans_riotid()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_8000000006", teamASide: "blue", gagnante: "B",
            equipeB: ["B1", "B2", "B3", "B4", "Fantome"])));

        var revue = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[0].GetProperty("rapportEnRevue");
        var inattendu = Assert.Single(revue.GetProperty("inattendus").EnumerateArray());
        Assert.Equal("puuid-Fantome#EUW", inattendu.GetProperty("puuid").GetString());
        Assert.Equal(JsonValueKind.Null, inattendu.GetProperty("riotId").ValueKind);
    }

    [Fact]
    public async Task Un_seul_joueur_de_chaque_equipe_sur_l_autre_side_met_le_rapport_en_revue()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        // A5 a été vu côté red et B5 côté blue : ce n'est pas un Side corrigé, les Équipes sont mêlées.
        var recu = await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_8000000004", teamASide: "blue", gagnante: "A",
            equipeA: ["A1", "A2", "A3", "A4", "B5"], equipeB: ["B1", "B2", "B3", "B4", "A5"])));

        Assert.Equal("EnRevue", recu.GetProperty("statut").GetString());
        var revue = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[0].GetProperty("rapportEnRevue");
        Assert.Equal(2, revue.GetProperty("mauvaisSide").GetArrayLength());
        Assert.Empty(revue.GetProperty("absents").EnumerateArray());
    }

    [Fact]
    public async Task Accepter_le_rapport_termine_la_game_comme_un_rapport_conforme()
    {
        var client = Factory.CreateSignedInClient();
        var enRevue = await MettreEnRevueAsync(client, gagnante: "B");

        var reponse = await client.PostAsync($"/rapports/{enRevue.RapportId}/accepter", null, Ct);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var recu = await ReadJsonAsync(reponse);
        Assert.Equal("Accepte", recu.GetProperty("statut").GetString());
        Assert.Equal(enRevue.GameId, recu.GetProperty("gameId").GetInt32());
        Assert.Equal("B", recu.GetProperty("winner").GetString());
        Assert.Equal(1, recu.GetProperty("teamBWins").GetInt32());

        var game = (await SerieAsync(client, enRevue.Serie.SeriesId)).GetProperty("games")[0];
        Assert.Equal("completed", game.GetProperty("status").GetString());
        Assert.Equal("B", game.GetProperty("winner").GetString());
        Assert.Equal("EUW1_8000000001", game.GetProperty("riotMatchId").GetString());
        Assert.Equal(JsonValueKind.Null, game.GetProperty("rapportEnRevue").ValueKind);
        // Les participants détectés gardent leur champion ; A5, absent du rapport, n'en a pas.
        var champions = game.GetProperty("championsJoues").EnumerateArray()
            .ToDictionary(c => c.GetProperty("playerId").GetInt32(), c => c.GetProperty("championId").GetString());
        Assert.Equal(9, champions.Count);
        Assert.Equal("Garen", champions[enRevue.Serie.TeamA[0]]);
        Assert.DoesNotContain(enRevue.Serie.TeamA[4], champions.Keys);
    }

    [Fact]
    public async Task Rejeter_le_rapport_l_ecarte_et_la_game_attend_un_autre_rapport_ou_une_saisie_manuelle()
    {
        var client = Factory.CreateSignedInClient();
        var enRevue = await MettreEnRevueAsync(client);

        var reponse = await client.PostAsync($"/rapports/{enRevue.RapportId}/rejeter", null, Ct);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var recu = await ReadJsonAsync(reponse);
        Assert.Equal("Rejete", recu.GetProperty("statut").GetString());
        Assert.Equal(JsonValueKind.Null, recu.GetProperty("winner").ValueKind);
        var game = (await SerieAsync(client, enRevue.Serie.SeriesId)).GetProperty("games")[0];
        Assert.Equal("pending", game.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, game.GetProperty("rapportEnRevue").ValueKind);

        var conforme = await ReadJsonAsync(await EnvoyerRapportAsync(enRevue.Code, Rapport("EUW1_8000000010", teamASide: "blue", gagnante: "A")));
        Assert.Equal("Accepte", conforme.GetProperty("statut").GetString());
        Assert.Equal(enRevue.GameId, conforme.GetProperty("gameId").GetInt32());
    }

    [Fact]
    public async Task Apres_un_rejet_la_saisie_manuelle_termine_la_game()
    {
        var client = Factory.CreateSignedInClient();
        var enRevue = await MettreEnRevueAsync(client);
        await ReadJsonAsync(await client.PostAsync($"/rapports/{enRevue.RapportId}/rejeter", null, Ct));

        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{enRevue.GameId}/result", new { winner = "B" }, Ct));

        Assert.Equal(1, (await SerieAsync(client, enRevue.Serie.SeriesId)).GetProperty("teamBWins").GetInt32());
    }

    [Fact]
    public async Task Une_decision_se_prend_une_seule_fois()
    {
        var client = Factory.CreateSignedInClient();
        var enRevue = await MettreEnRevueAsync(client);
        await ReadJsonAsync(await client.PostAsync($"/rapports/{enRevue.RapportId}/rejeter", null, Ct));

        await AssertRefusAsync(await client.PostAsync($"/rapports/{enRevue.RapportId}/accepter", null, Ct), HttpStatusCode.Conflict);
        await AssertRefusAsync(await client.PostAsync($"/rapports/{enRevue.RapportId}/rejeter", null, Ct), HttpStatusCode.Conflict);
        await AssertRefusAsync(await client.PostAsync("/rapports/999999/accepter", null, Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Accepter_et_rejeter_en_meme_temps_une_seule_decision_l_emporte()
    {
        var client = Factory.CreateSignedInClient();
        var enRevue = await MettreEnRevueAsync(client);

        var reponses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => client.PostAsync(
            $"/rapports/{enRevue.RapportId}/{(i % 2 == 0 ? "accepter" : "rejeter")}", null, Ct)));

        Assert.Single(reponses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.All(reponses.Where(r => r.StatusCode != HttpStatusCode.OK),
            r => Assert.Equal(HttpStatusCode.Conflict, r.StatusCode));
    }

    [Fact]
    public async Task Seul_un_utilisateur_connecte_decide_pas_le_code_companion()
    {
        var client = Factory.CreateSignedInClient();
        var enRevue = await MettreEnRevueAsync(client);
        var anonyme = Factory.CreateAnonymousClient();

        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonyme.PostAsync($"/rapports/{enRevue.RapportId}/accepter", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonyme.PostAsync($"/rapports/{enRevue.RapportId}/rejeter", null, Ct)).StatusCode);
        Assert.Equal("pending", (await SerieAsync(client, enRevue.Serie.SeriesId)).GetProperty("games")[0].GetProperty("status").GetString());
    }

    [Fact]
    public async Task Pendant_la_revue_la_game_suivante_la_draft_et_la_saisie_manuelle_sont_refusees_puis_permises_apres_acceptation()
    {
        var client = Factory.CreateSignedInClient();
        var enRevue = await MettreEnRevueAsync(client);
        var seriesId = enRevue.Serie.SeriesId;
        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{seriesId}/capitaines/A",
            new { designation = "Manuel", playerId = enRevue.Serie.TeamA[0] }, Ct));
        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{seriesId}/capitaines/B",
            new { designation = "Manuel", playerId = enRevue.Serie.TeamB[0] }, Ct));

        await RejoindreLesCapitainesAsync(client, seriesId);
        var liens = await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}/liens", Ct));
        var lienA = Uri.EscapeDataString(liens.GetProperty("lienCapitaineA").GetString()!);
        await AssertRefusAsync(await Factory.CreateAnonymousClient().PutAsJsonAsync($"/liens/{lienA}/pret", new { pret = true }, Ct),
            HttpStatusCode.Conflict);
        await AssertRefusAsync(await client.PutAsJsonAsync($"/games/{enRevue.GameId}/result", new { winner = "B" }, Ct), HttpStatusCode.Conflict);

        await ReadJsonAsync(await client.PostAsync($"/rapports/{enRevue.RapportId}/accepter", null, Ct));

        var suivante = await PlayGameAsync(client, seriesId, teamASide: "red");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/games/{suivante}/draft-champions", Ct)).StatusCode);
    }

    [Fact]
    public async Task Un_nouveau_rapport_pour_une_game_en_revue_est_refuse_sauf_le_meme_qui_est_dedoublonne()
    {
        var client = Factory.CreateSignedInClient();
        var enRevue = await MettreEnRevueAsync(client);

        var autre = await EnvoyerRapportAsync(enRevue.Code, Rapport("EUW1_8000000020", teamASide: "blue", gagnante: "A"));
        var memeIdentifiant = await EnvoyerRapportAsync(enRevue.Code, Rapport("EUW1_8000000001", teamASide: "blue", gagnante: "A",
            equipeA: ["A1", "A2", "A3", "A4", "X1"]));

        await AssertRefusAsync(autre, HttpStatusCode.Conflict);
        Assert.Equal(HttpStatusCode.OK, memeIdentifiant.StatusCode);
        var recu = await ReadJsonAsync(memeIdentifiant);
        Assert.Equal("EnRevue", recu.GetProperty("statut").GetString());
        Assert.Equal(enRevue.GameId, recu.GetProperty("gameId").GetInt32());
        var game = (await SerieAsync(client, enRevue.Serie.SeriesId)).GetProperty("games")[0];
        Assert.Equal("pending", game.GetProperty("status").GetString());
        Assert.Equal(enRevue.RapportId, game.GetProperty("rapportEnRevue").GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task Renvoyer_un_rapport_rejete_ne_cree_rien_et_repond_rejete()
    {
        var client = Factory.CreateSignedInClient();
        var enRevue = await MettreEnRevueAsync(client);
        await ReadJsonAsync(await client.PostAsync($"/rapports/{enRevue.RapportId}/rejeter", null, Ct));

        var renvoi = await EnvoyerRapportAsync(enRevue.Code, Rapport("EUW1_8000000001", teamASide: "blue", gagnante: "A",
            equipeA: ["A1", "A2", "A3", "A4", "X1"]));

        Assert.Equal(HttpStatusCode.OK, renvoi.StatusCode);
        var recu = await ReadJsonAsync(renvoi);
        Assert.Equal("Rejete", recu.GetProperty("statut").GetString());
        Assert.Equal(enRevue.GameId, recu.GetProperty("gameId").GetInt32());
        Assert.Equal(JsonValueKind.Null, recu.GetProperty("winner").ValueKind);
        var game = (await SerieAsync(client, enRevue.Serie.SeriesId)).GetProperty("games")[0];
        Assert.Equal("pending", game.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, game.GetProperty("rapportEnRevue").ValueKind);
        // La game n'est pas bloquée à nouveau : la saisie manuelle passe.
        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{enRevue.GameId}/result", new { winner = "A" }, Ct));
    }

    [Fact]
    public async Task Le_meme_rapport_en_ecart_envoye_simultanement_n_est_mis_en_revue_qu_une_fois()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        await CreatePlayerAsync(client, "X1");
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        var reponses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => EnvoyerRapportAsync(code,
            Rapport("EUW1_8000000040", teamASide: "blue", gagnante: "A", equipeA: ["A1", "A2", "A3", "A4", "X1"], emetteur: i))));

        foreach (var reponse in reponses)
        {
            var recu = await ReadJsonAsync(reponse);
            Assert.Equal("EnRevue", recu.GetProperty("statut").GetString());
            Assert.Equal(gameId, recu.GetProperty("gameId").GetInt32());
        }
        var game = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[0];
        Assert.Equal("EUW1_8000000040", game.GetProperty("rapportEnRevue").GetProperty("riotMatchId").GetString());
    }

    [Fact]
    public async Task Renvoyer_un_rapport_rejete_apres_la_cloture_de_la_serie_repond_rejete()
    {
        var client = Factory.CreateSignedInClient();
        var enRevue = await MettreEnRevueAsync(client);
        await ReadJsonAsync(await client.PostAsync($"/rapports/{enRevue.RapportId}/rejeter", null, Ct));
        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{enRevue.GameId}/result", new { winner = "A" }, Ct));
        await PlayAndWinAsync(client, enRevue.Serie.SeriesId, "A");

        var renvoi = await EnvoyerRapportAsync(enRevue.Code, Rapport("EUW1_8000000001", teamASide: "blue", gagnante: "A",
            equipeA: ["A1", "A2", "A3", "A4", "X1"]));

        Assert.Equal(HttpStatusCode.OK, renvoi.StatusCode);
        var recu = await ReadJsonAsync(renvoi);
        Assert.Equal("Rejete", recu.GetProperty("statut").GetString());
        Assert.Equal(enRevue.GameId, recu.GetProperty("gameId").GetInt32());
        Assert.Equal("Gagnee", recu.GetProperty("outcome").GetString());
    }

    [Fact]
    public async Task Interrompre_la_serie_pendant_la_revue_fait_disparaitre_le_rapport_avec_la_game_en_attente()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_8000000030", teamASide: "blue", gagnante: "A", equipeA: ["A1", "A2", "A3", "A4", "X1"])));
        var rapportId = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[1]
            .GetProperty("rapportEnRevue").GetProperty("id").GetInt32();

        var arret = await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        Assert.Equal("Interrompue", arret.GetProperty("arret").GetString());
        Assert.Single((await SerieAsync(client, serie.SeriesId)).GetProperty("games").EnumerateArray());
        await AssertRefusAsync(await client.PostAsync($"/rapports/{rapportId}/accepter", null, Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Annuler_la_serie_pendant_la_revue_efface_le_rapport()
    {
        var client = Factory.CreateSignedInClient();
        var enRevue = await MettreEnRevueAsync(client);

        var arret = await ReadJsonAsync(await client.PostAsync($"/series/{enRevue.Serie.SeriesId}/stop", null, Ct));

        Assert.Equal("Annulee", arret.GetProperty("arret").GetString());
        await AssertRefusAsync(await client.PostAsync($"/rapports/{enRevue.RapportId}/rejeter", null, Ct), HttpStatusCode.NotFound);
    }
}
