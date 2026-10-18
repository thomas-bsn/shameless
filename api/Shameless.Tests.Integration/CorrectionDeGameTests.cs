using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// Corriger ou supprimer une game terminée (ADR 0009) : réservé aux Administrateurs ; la Série (score,
/// issue) et le cost du mois sont recalculés ; la game porte « modifiée par un admin ».
/// </summary>
public class CorrectionDeGameTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static readonly DateTimeOffset Mars = new(2026, 3, 10, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Avril = new(2026, 4, 7, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Corriger_ou_supprimer_une_game_terminee_est_reserve_aux_administrateurs()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");

        var utilisateur = await CorrigerAsync(client, gameId, new { winner = "B" });
        var anonyme = await CorrigerAsync(Factory.CreateAnonymousClient(), gameId, new { winner = "B" });
        var suppressionUtilisateur = await client.DeleteAsync($"/games/{gameId}", Ct);
        var suppressionAnonyme = await Factory.CreateAnonymousClient().DeleteAsync($"/games/{gameId}", Ct);

        Assert.Equal(HttpStatusCode.Forbidden, utilisateur.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonyme.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, suppressionUtilisateur.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, suppressionAnonyme.StatusCode);
        var game = await GameAsync(client, gameId);
        Assert.Equal("A", game.GetProperty("winner").GetString());
        Assert.Equal(JsonValueKind.Null, game.GetProperty("modifieeParAdminLe").ValueKind);
    }

    [Fact]
    public async Task Saisir_le_resultat_d_une_game_en_cours_reste_ouvert_a_tout_utilisateur_connecte()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId);

        var reponse = await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct);
        var arret = await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, arret.StatusCode);
    }

    [Fact]
    public async Task Corriger_le_vainqueur_d_une_game_d_une_serie_multi_games_recalcule_le_score_et_marque_la_game()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var game1 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        var game2 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        Assert.Equal("Gagnee", (await SerieAsync(client, serie.SeriesId)).GetProperty("outcome").GetString());

        var reponse = await CorrigerAsync(admin, game2, new { winner = "B" });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal("EnCours", lue.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, lue.GetProperty("winningTeam").ValueKind);
        Assert.Equal((1, 1), (lue.GetProperty("teamAWins").GetInt32(), lue.GetProperty("teamBWins").GetInt32()));
        var corrigee = await GameAsync(client, game2);
        Assert.Equal("B", corrigee.GetProperty("winner").GetString());
        Assert.NotEqual(JsonValueKind.Null, corrigee.GetProperty("modifieeParAdminLe").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await GameAsync(client, game1)).GetProperty("modifieeParAdminLe").ValueKind);

        // La Série rouverte se rejoue et se termine normalement.
        await PlayAndWinAsync(client, serie.SeriesId, "B");
        var terminee = await SerieAsync(client, serie.SeriesId);
        Assert.Equal("Gagnee", terminee.GetProperty("outcome").GetString());
        Assert.Equal("B", terminee.GetProperty("winningTeam").GetString());
    }

    [Fact]
    public async Task Corriger_la_derniere_game_d_une_serie_deja_gagnee_change_son_vainqueur()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");

        await ReadJsonAsync(await CorrigerAsync(Factory.CreateAdminClient(), gameId, new { winner = "B" }));

        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal("Gagnee", lue.GetProperty("outcome").GetString());
        Assert.Equal("B", lue.GetProperty("winningTeam").GetString());
        Assert.Equal((0, 1), (lue.GetProperty("teamAWins").GetInt32(), lue.GetProperty("teamBWins").GetInt32()));
    }

    [Fact]
    public async Task Corriger_une_game_d_une_serie_interrompue_met_le_score_a_jour_et_la_garde_interrompue()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5);
        var game1 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        await ReadJsonAsync(await CorrigerAsync(Factory.CreateAdminClient(), game1, new { winner = "B" }));

        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal("Interrompue", lue.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, lue.GetProperty("winningTeam").ValueKind);
        Assert.Equal((1, 1), (lue.GetProperty("teamAWins").GetInt32(), lue.GetProperty("teamBWins").GetInt32()));
    }

    [Fact]
    public async Task Une_correction_sans_changement_ne_marque_pas_la_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");

        var reponse = await CorrigerAsync(Factory.CreateAdminClient(), gameId,
            new { winner = "A", teamA = serie.TeamA, teamB = serie.TeamB });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await GameAsync(client, gameId)).GetProperty("modifieeParAdminLe").ValueKind);
    }

    [Fact]
    public async Task Corriger_la_composition_remplace_un_joueur_et_recalcule_le_cost_des_deux()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        var remplacant = await CreatePlayerAsync(client, "Remplacant");
        var game = await PlayAndWinAsync(client, serie.SeriesId, "A");
        Assert.Equal(1, (await CostAsync(client, serie.TeamA[4])).GetProperty("gamesTerminees").GetInt32());

        var equipeA = serie.TeamA[..4].Append(remplacant).ToArray();
        var reponse = await CorrigerAsync(admin, game, new { teamA = equipeA, teamB = serie.TeamB });

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var lue = await GameAsync(client, game);
        Assert.Equal(equipeA.Order(), Equipe(lue, "teamA").Order());
        Assert.Equal(serie.TeamB.Order(), Equipe(lue, "teamB").Order());
        Assert.Equal("A", lue.GetProperty("winner").GetString());
        Assert.NotEqual(JsonValueKind.Null, lue.GetProperty("modifieeParAdminLe").ValueKind);
        Assert.Equal(0, (await CostAsync(client, serie.TeamA[4])).GetProperty("gamesTerminees").GetInt32());
        var cost = await CostAsync(client, remplacant);
        Assert.Equal((1, 1), (cost.GetProperty("gamesTerminees").GetInt32(), cost.GetProperty("victoires").GetInt32()));
    }

    [Fact]
    public async Task Corriger_la_composition_peut_changer_un_joueur_d_equipe()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var game = await PlayAndWinAsync(client, serie.SeriesId, "A", teamASide: "red");

        var equipeA = serie.TeamA[..4].Append(serie.TeamB[0]).ToArray();
        var equipeB = serie.TeamB[1..].Append(serie.TeamA[4]).ToArray();
        await ReadJsonAsync(await CorrigerAsync(Factory.CreateAdminClient(), game, new { teamA = equipeA, teamB = equipeB }));

        var lue = await GameAsync(client, game);
        Assert.Equal(equipeA.Order(), Equipe(lue, "teamA").Order());
        Assert.Equal(equipeB.Order(), Equipe(lue, "teamB").Order());
        Assert.Equal("red", lue.GetProperty("teamASide").GetString());
        var ancienB1 = await CostAsync(client, serie.TeamB[0]);
        Assert.Equal(1, ancienB1.GetProperty("victoires").GetInt32());
    }

    [Fact]
    public async Task Une_game_avec_rapport_companion_garde_ses_donnees_riot_et_les_champions_des_joueurs_conserves()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var remplacant = await CreatePlayerAsync(client, "Remplacant");
        var gameId = await PlayGameAsync(client, serie.SeriesId, "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_8100000001", "blue", "A")));

        var equipeA = serie.TeamA[..4].Append(remplacant).ToArray();
        await ReadJsonAsync(await CorrigerAsync(Factory.CreateAdminClient(), gameId,
            new { winner = "B", teamA = equipeA, teamB = serie.TeamB }));

        var lue = await GameAsync(client, gameId);
        Assert.Equal("EUW1_8100000001", lue.GetProperty("riotMatchId").GetString());
        Assert.Equal("B", lue.GetProperty("winner").GetString());
        var champions = lue.GetProperty("teamA").EnumerateArray()
            .ToDictionary(p => p.GetProperty("playerId").GetInt32(), p => p.GetProperty("championId").GetString());
        Assert.Equal("Garen", champions[serie.TeamA[0]]);
        Assert.Null(champions[remplacant]);
    }

    [Theory]
    [InlineData("vainqueur")]
    [InlineData("rien")]
    [InlineData("une-seule-equipe")]
    [InlineData("quatre-joueurs")]
    [InlineData("doublon")]
    [InlineData("joueur-inconnu")]
    [InlineData("joueur-dans-les-deux-equipes")]
    public async Task Une_correction_invalide_repond_400_et_ne_change_rien(string cas)
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");
        object corps = cas switch
        {
            "vainqueur" => new { winner = "C" },
            "rien" => new { },
            "une-seule-equipe" => new { teamA = serie.TeamA },
            "quatre-joueurs" => new { teamA = serie.TeamA[..4], teamB = serie.TeamB },
            "doublon" => new { teamA = serie.TeamA[..4].Append(serie.TeamA[0]).ToArray(), teamB = serie.TeamB },
            "joueur-inconnu" => new { teamA = serie.TeamA[..4].Append(99999).ToArray(), teamB = serie.TeamB },
            _ => new { teamA = serie.TeamA[..4].Append(serie.TeamB[0]).ToArray(), teamB = serie.TeamB }
        };

        var reponse = await CorrigerAsync(Factory.CreateAdminClient(), gameId, corps);

        await AssertRefusAsync(reponse, HttpStatusCode.BadRequest);
        var game = await GameAsync(client, gameId);
        Assert.Equal("A", game.GetProperty("winner").GetString());
        Assert.Equal(serie.TeamA.Order(), Equipe(game, "teamA").Order());
        Assert.Equal(JsonValueKind.Null, game.GetProperty("modifieeParAdminLe").ValueKind);
    }

    [Fact]
    public async Task Corriger_ou_supprimer_une_game_en_attente_ou_inconnue_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var enAttente = await PlayGameAsync(client, serie.SeriesId);

        await AssertRefusAsync(await CorrigerAsync(admin, enAttente, new { winner = "A" }), HttpStatusCode.Conflict);
        await AssertRefusAsync(await admin.DeleteAsync($"/games/{enAttente}", Ct), HttpStatusCode.Conflict);
        await AssertRefusAsync(await CorrigerAsync(admin, 99999, new { winner = "A" }), HttpStatusCode.NotFound);
        await AssertRefusAsync(await admin.DeleteAsync("/games/99999", Ct), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Supprimer_une_game_d_une_serie_multi_games_recalcule_le_score_et_l_issue()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var game1 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        Assert.Equal("Gagnee", (await SerieAsync(client, serie.SeriesId)).GetProperty("outcome").GetString());

        var reponse = await Factory.CreateAdminClient().DeleteAsync($"/games/{game1}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, reponse.StatusCode);
        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal("EnCours", lue.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, lue.GetProperty("winningTeam").ValueKind);
        Assert.Equal((1, 0), (lue.GetProperty("teamAWins").GetInt32(), lue.GetProperty("teamBWins").GetInt32()));
        Assert.Single(lue.GetProperty("games").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/games/{game1}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Supprimer_une_game_d_une_serie_interrompue_la_garde_interrompue()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5);
        var game1 = await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayAndWinAsync(client, serie.SeriesId, "B");
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        await Factory.CreateAdminClient().DeleteAsync($"/games/{game1}", Ct);

        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal("Interrompue", lue.GetProperty("outcome").GetString());
        Assert.Equal((0, 1), (lue.GetProperty("teamAWins").GetInt32(), lue.GetProperty("teamBWins").GetInt32()));
    }

    [Fact]
    public async Task Supprimer_la_derniere_game_terminee_efface_la_serie_comme_une_serie_annulee()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "A");
        var enAttente = await PlayGameAsync(client, serie.SeriesId);

        var reponse = await Factory.CreateAdminClient().DeleteAsync($"/games/{gameId}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, reponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/series/{serie.SeriesId}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/games/{enAttente}", Ct)).StatusCode);
        Assert.Empty((await ReadJsonAsync(await client.GetAsync("/games/pending", Ct))).EnumerateArray());
        Assert.Empty((await ReadJsonAsync(await client.GetAsync("/series/active", Ct))).GetProperty("series").EnumerateArray());
        Assert.Equal(0, (await CostAsync(client, serie.TeamA[0])).GetProperty("gamesTerminees").GetInt32());
    }

    [Fact]
    public async Task Supprimer_une_game_d_une_serie_deja_terminee_efface_la_serie_si_c_etait_sa_seule_game()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var gameId = await PlayAndWinAsync(client, serie.SeriesId, "B");

        await Factory.CreateAdminClient().DeleteAsync($"/games/{gameId}", Ct);

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/series/{serie.SeriesId}", Ct)).StatusCode);
    }

    [Fact]
    public async Task Corriger_le_vainqueur_recalcule_le_cost_du_mois_courant()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient(); // avant de déplacer l'horloge : la session expirerait
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        // A1 noté 79 à chaque game, 3 victoires sur 5 : 2 + 0,406 + 0,06 = 2,466 → 2,5.
        var games = new List<int>();
        foreach (var gagnante in new[] { "A", "A", "B", "B", "A" })
            games.Add(await JouerUneGameNoteeAsync(client, serie.SeriesId, gagnante));
        Assert.Equal(2.5m, (await CostAsync(client, serie.TeamA[0])).GetProperty("cost").GetDecimal());

        // 4 victoires sur 5 : 2 + 0,406 + 0,18 = 2,586 → 2,6.
        await ReadJsonAsync(await CorrigerAsync(admin, games[2], new { winner = "A" }));

        var cost = await CostAsync(client, serie.TeamA[0]);
        Assert.Equal(2.6m, cost.GetProperty("cost").GetDecimal());
        Assert.Equal(4, cost.GetProperty("victoires").GetInt32());
        Assert.Equal(2.6m, (await ReadJsonAsync(await client.GetAsync($"/players/{serie.TeamA[0]}", Ct)))
            .GetProperty("currentCost").GetDecimal());
    }

    [Fact]
    public async Task Corriger_une_game_d_un_mois_passe_recalcule_ce_mois_et_les_suivants()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient(); // avant de déplacer l'horloge : la session expirerait
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        var games = new List<int>();
        foreach (var gagnante in new[] { "A", "A", "B", "B", "A" })
            games.Add(await JouerUneGameNoteeAsync(client, serie.SeriesId, gagnante));
        Factory.Clock.SetUtcNow(Avril);
        Assert.Equal(2.5m, (await CostAsync(client, serie.TeamA[0])).GetProperty("costDeDepart").GetDecimal());

        await ReadJsonAsync(await CorrigerAsync(admin, games[2], new { winner = "A" }));

        var historique = await HistoriqueAsync(client, serie.TeamA[0]);
        var mars = historique.Single(m => m.GetProperty("mois").GetString() == "2026-03-01");
        Assert.Equal((2.6m, 4), (mars.GetProperty("cost").GetDecimal(), mars.GetProperty("victoires").GetInt32()));
        var avril = await CostAsync(client, serie.TeamA[0]);
        Assert.Equal(2.6m, avril.GetProperty("costDeDepart").GetDecimal());
        Assert.Equal(2.6m, avril.GetProperty("cost").GetDecimal());
    }

    [Fact]
    public async Task Supprimer_une_game_du_mois_passe_sous_le_seuil_de_cinq_games_ramene_le_cost_au_depart()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient(); // avant de déplacer l'horloge : la session expirerait
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        var games = new List<int>();
        foreach (var gagnante in new[] { "A", "A", "B", "B", "A" })
            games.Add(await JouerUneGameNoteeAsync(client, serie.SeriesId, gagnante));
        Factory.Clock.SetUtcNow(Avril);

        await admin.DeleteAsync($"/games/{games[4]}", Ct);

        var historique = await HistoriqueAsync(client, serie.TeamA[0]);
        var mars = historique.Single(m => m.GetProperty("mois").GetString() == "2026-03-01");
        Assert.Equal((2m, 4), (mars.GetProperty("cost").GetDecimal(), mars.GetProperty("gamesTerminees").GetInt32()));
        Assert.Equal(2m, (await CostAsync(client, serie.TeamA[0])).GetProperty("cost").GetDecimal());
        Assert.Equal(2m, (await ReadJsonAsync(await client.GetAsync($"/players/{serie.TeamA[0]}", Ct)))
            .GetProperty("currentCost").GetDecimal());
    }

    [Fact]
    public async Task Supprimer_une_game_avec_rapport_companion_la_retire_et_libere_l_identifiant_riot()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_8100000002", "blue", "A")));
        await PlayAndWinAsync(client, serie.SeriesId, "B");

        var reponse = await Factory.CreateAdminClient().DeleteAsync($"/games/{gameId}", Ct);

        Assert.Equal(HttpStatusCode.NoContent, reponse.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/games/{gameId}", Ct)).StatusCode);
        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal((0, 1), (lue.GetProperty("teamAWins").GetInt32(), lue.GetProperty("teamBWins").GetInt32()));
    }

    private static Task<HttpResponseMessage> CorrigerAsync(HttpClient client, int gameId, object corps) =>
        client.PutAsJsonAsync($"/games/{gameId}/correction", corps, Ct);

    private async Task<JsonElement> GameAsync(HttpClient client, int gameId) =>
        await ReadJsonAsync(await client.GetAsync($"/games/{gameId}", Ct));

    private async Task<JsonElement> SerieAsync(HttpClient client, int seriesId) =>
        await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));

    private static int[] Equipe(JsonElement game, string equipe) =>
        game.GetProperty(equipe).EnumerateArray().Select(p => p.GetProperty("playerId").GetInt32()).ToArray();

    private async Task<JsonElement> CostAsync(HttpClient client, int playerId) =>
        await ReadJsonAsync(await client.GetAsync($"/players/{playerId}/cost", Ct));

    private async Task<List<JsonElement>> HistoriqueAsync(HttpClient client, int playerId) =>
        (await ReadJsonAsync(await client.GetAsync($"/players/{playerId}/cost/historique", Ct)))
            .GetProperty("mois").EnumerateArray().ToList();

    private int _rapports;

    /// <summary>Lance une game et la termine par un rapport sans durée : A1 y est noté 79 (ADR 0011).</summary>
    private async Task<int> JouerUneGameNoteeAsync(HttpClient client, int seriesId, string gagnante)
    {
        var gameId = await PlayGameAsync(client, seriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, seriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport($"EUW1_8200{_rapports++:D5}", "blue", gagnante,
            donnees: new { gameMode = "CLASSIC" })));
        return gameId;
    }
}
