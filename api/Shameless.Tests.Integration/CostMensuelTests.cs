using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// Le cost mensuel (ADR 0012) : départ depuis le mois précédent ou le Rang SoloQ, évolution à partir
/// de cinq Games terminées dans le mois, historique conservé dans PlayerMonthlyCost.
/// </summary>
public class CostMensuelTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static readonly DateTimeOffset Mars = new(2026, 3, 10, 20, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Avril = new(2026, 4, 7, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Sans_historique_le_cost_part_du_rang_soloq_et_le_winrate_seul_ne_le_fait_pas_evoluer()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5); // tous GOLD II : cost de départ 2

        var depart = await CostAsync(client, serie.TeamA[0]);
        Assert.Equal(2m, depart.GetProperty("cost").GetDecimal());
        Assert.Equal(2m, depart.GetProperty("costDeDepart").GetDecimal());
        Assert.Equal("RangSoloQ", depart.GetProperty("sourceDuCostDeDepart").GetString());
        Assert.Equal(2m, (await PlayerAsync(client, serie.TeamA[0])).GetProperty("currentCost").GetDecimal());

        foreach (var gagnante in new[] { "A", "A", "B", "B" })
            await PlayAndWinAsync(client, serie.SeriesId, gagnante);

        var avantSeuil = await CostAsync(client, serie.TeamA[0]);
        Assert.Equal(4, avantSeuil.GetProperty("gamesTerminees").GetInt32());
        Assert.False(avantSeuil.GetProperty("evolue").GetBoolean());
        Assert.Equal(2m, avantSeuil.GetProperty("cost").GetDecimal());

        await PlayAndWinAsync(client, serie.SeriesId, "A");

        // Games saisies à la main, sans note : 3 victoires sur 5 (60 %), mais sans note de
        // performance le winrate seul ne fait pas évoluer le cost.
        var a1 = await CostAsync(client, serie.TeamA[0]);
        Assert.False(a1.GetProperty("evolue").GetBoolean());
        Assert.Equal(60, a1.GetProperty("winrate").GetDouble());
        Assert.Equal(JsonValueKind.Null, a1.GetProperty("noteMoyenne").ValueKind);
        Assert.Equal(0m, a1.GetProperty("ajustementResultats").GetDecimal());
        Assert.Equal(2m, a1.GetProperty("cost").GetDecimal());
        Assert.Equal(2m, (await PlayerAsync(client, serie.TeamA[0])).GetProperty("currentCost").GetDecimal());
        Assert.Equal(2m, (await CostAsync(client, serie.TeamB[0])).GetProperty("cost").GetDecimal());
    }

    [Fact]
    public async Task Le_nouveau_mois_part_du_cost_final_du_mois_precedent_sans_effacer_son_historique()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        // A1 noté 79 à chaque game, 3 victoires sur 5 : 2 + 0,406 + 0,06 = 2,466 → 2,5.
        foreach (var gagnante in new[] { "A", "A", "B", "B", "A" })
            await JouerUneGameNoteeAsync(client, serie.SeriesId, gagnante);

        Factory.Clock.SetUtcNow(Avril);
        var debutAvril = await CostAsync(client, serie.TeamA[0]);
        Assert.Equal("2026-04-01", debutAvril.GetProperty("mois").GetString());
        Assert.Equal(2.5m, debutAvril.GetProperty("costDeDepart").GetDecimal());
        Assert.Equal("MoisPrecedent", debutAvril.GetProperty("sourceDuCostDeDepart").GetString());
        Assert.Equal(0, debutAvril.GetProperty("gamesTerminees").GetInt32());
        Assert.Equal(2.5m, debutAvril.GetProperty("cost").GetDecimal());

        // En avril, A gagne une game : moins de cinq games, le cost reste celui de départ.
        var revanche = await RevancheAsync(client, serie.SeriesId);
        await PlayAndWinAsync(client, revanche, "A");

        var historique = await HistoriqueAsync(client, serie.TeamA[0]);
        Assert.Equal(
            [("2026-03-01", 2m, 2.5m, 5, 3), ("2026-04-01", 2.5m, 2.5m, 1, 1)],
            historique.Select(m => (
                m.GetProperty("mois").GetString(),
                m.GetProperty("costDeDepart").GetDecimal(),
                m.GetProperty("cost").GetDecimal(),
                m.GetProperty("gamesTerminees").GetInt32(),
                m.GetProperty("victoires").GetInt32())));
    }

    [Fact]
    public async Task La_liste_des_joueurs_donne_l_evolution_du_cost_depuis_le_cost_final_du_mois_precedent()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        // A1 finit mars à 2,5 (voir le test du nouveau mois).
        foreach (var gagnante in new[] { "A", "A", "B", "B", "A" })
            await JouerUneGameNoteeAsync(client, serie.SeriesId, gagnante);
        var nouveau = await CreatePlayerAsync(client, "Nouveau");

        // Pendant le mois de mars, aucun mois précédent : pas d'évolution connue.
        Assert.Equal(JsonValueKind.Null, (await JoueurDeLaListeAsync(client, serie.TeamA[0])).GetProperty("costEvolution").ValueKind);

        Factory.Clock.SetUtcNow(Avril);
        Assert.Equal(0m, (await JoueurDeLaListeAsync(client, serie.TeamA[0])).GetProperty("costEvolution").GetDecimal());

        await ReadJsonAsync(await admin.PutAsJsonAsync($"/players/{serie.TeamA[0]}/cost", new { cost = 3.5m }, Ct));
        var a1 = await JoueurDeLaListeAsync(client, serie.TeamA[0]);
        Assert.Equal(3.5m, a1.GetProperty("currentCost").GetDecimal());
        Assert.Equal(1m, a1.GetProperty("costEvolution").GetDecimal());

        // Un joueur sans historique n'a pas d'évolution.
        Assert.Equal(JsonValueKind.Null, (await JoueurDeLaListeAsync(client, nouveau)).GetProperty("costEvolution").ValueKind);
    }

    [Fact]
    public async Task L_evolution_du_cost_est_inconnue_quand_le_mois_precedent_n_a_pas_de_ligne()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        foreach (var gagnante in new[] { "A", "A", "B", "B", "A" })
            await JouerUneGameNoteeAsync(client, serie.SeriesId, gagnante);

        // En mai, le mois précédent est avril, sans ligne : mars ne sert pas de repli.
        Factory.Clock.SetUtcNow(new DateTimeOffset(2026, 5, 12, 20, 0, 0, TimeSpan.Zero));

        Assert.Equal(JsonValueKind.Null, (await JoueurDeLaListeAsync(client, serie.TeamA[0])).GetProperty("costEvolution").ValueKind);
    }

    [Fact]
    public async Task Le_cost_de_depart_est_le_dernier_cost_final_conserve_meme_apres_un_mois_sans_game()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        // A1 noté 79 à chaque game, 2 victoires sur 5 : 2 + 0,406 − 0,06 = 2,346 → 2,3.
        foreach (var gagnante in new[] { "B", "B", "A", "A", "B" })
            await JouerUneGameNoteeAsync(client, serie.SeriesId, gagnante);

        Factory.Clock.SetUtcNow(new DateTimeOffset(2026, 5, 12, 20, 0, 0, TimeSpan.Zero));

        var mai = await CostAsync(client, serie.TeamA[0]);
        Assert.Equal("2026-05-01", mai.GetProperty("mois").GetString());
        Assert.Equal(2.3m, mai.GetProperty("costDeDepart").GetDecimal());
        Assert.Equal("MoisPrecedent", mai.GetProperty("sourceDuCostDeDepart").GetString());
    }

    [Fact]
    public async Task Les_games_terminees_d_une_serie_interrompue_comptent_mais_pas_une_game_non_terminee()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5);
        foreach (var gagnante in new[] { "A", "A", "B" })
            await PlayAndWinAsync(client, serie.SeriesId, gagnante);
        await PlayGameAsync(client, serie.SeriesId);
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        var revanche = await RevancheAsync(client, serie.SeriesId);
        await PlayAndWinAsync(client, revanche, "A");
        await PlayAndWinAsync(client, revanche, "B");
        await PlayGameAsync(client, revanche); // en attente : ne compte pas

        // 5 games terminées dont 3 de la Série interrompue ; A en gagne 3.
        var a1 = await CostAsync(client, serie.TeamA[0]);
        Assert.Equal(5, a1.GetProperty("gamesTerminees").GetInt32());
        Assert.Equal(3, a1.GetProperty("victoires").GetInt32());
    }

    [Fact]
    public async Task La_note_de_performance_compte_plus_que_les_defaites()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5);

        // Rapport par défaut sans durée : A1 (0/2/7, KP 70 %) est noté 79 à chaque game (ADR 0011).
        // L'Équipe A perd les cinq games : trois dans le BO5, deux dans la revanche.
        var seriesId = serie.SeriesId;
        for (var i = 0; i < 5; i++)
        {
            if (i == 3) seriesId = await RevancheAsync(client, seriesId);
            if (i == 4) Assert.Equal(2m, (await CostAsync(client, serie.TeamA[0])).GetProperty("cost").GetDecimal());
            await JouerUneGameNoteeAsync(client, seriesId, "B");
        }

        // Performance : 0,7 × (79 − 50) / 50 = +0,406 ; résultats : 0 % de victoires = −0,3 → 2,106.
        var a1 = await CostAsync(client, serie.TeamA[0]);
        Assert.Equal(5, a1.GetProperty("gamesNotees").GetInt32());
        Assert.Equal(79, a1.GetProperty("noteMoyenne").GetDouble());
        Assert.Equal(0.41m, a1.GetProperty("ajustementPerformance").GetDecimal());
        Assert.Equal(-0.3m, a1.GetProperty("ajustementResultats").GetDecimal());
        Assert.Equal(2.1m, a1.GetProperty("cost").GetDecimal());
    }

    [Fact]
    public async Task Recalculer_un_mois_recalcule_en_cascade_les_mois_suivants_deja_enregistres()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        foreach (var gagnante in new[] { "A", "A", "B", "B" })
            await JouerUneGameNoteeAsync(client, serie.SeriesId, gagnante);
        await PlayGameAsync(client, serie.SeriesId); // lancée en mars, terminée en avril

        // En avril, les mêmes joueurs jouent une autre Série avant la fin de la game de mars.
        Factory.Clock.SetUtcNow(Avril);
        var avril = await NouvelleSerieAsync(client, serie);
        await PlayAndWinAsync(client, avril, "A");
        await ReadJsonAsync(await admin.PutAsJsonAsync($"/players/{serie.TeamB[0]}/cost", new { cost = 4m }, Ct));
        Assert.Equal(2m, (await CostAsync(client, serie.TeamA[0])).GetProperty("costDeDepart").GetDecimal());

        // La cinquième game de mars se termine : mars évolue (2,5), avril repart de ce cost final.
        await EnvoyerUnRapportNoteAsync(client, serie.SeriesId, "A");

        var historique = await HistoriqueAsync(client, serie.TeamA[0]);
        Assert.Equal(
            [("2026-03-01", 2m, 2.5m), ("2026-04-01", 2.5m, 2.5m)],
            historique.Select(m => (
                m.GetProperty("mois").GetString(),
                m.GetProperty("costDeDepart").GetDecimal(),
                m.GetProperty("cost").GetDecimal())));
        Assert.Equal(2.5m, (await PlayerAsync(client, serie.TeamA[0])).GetProperty("currentCost").GetDecimal());

        // Le cost fixé par un admin en avril reste le départ d'avril.
        var b1 = await CostAsync(client, serie.TeamB[0]);
        Assert.Equal(4m, b1.GetProperty("costDeDepart").GetDecimal());
        Assert.Equal("FixeParAdmin", b1.GetProperty("sourceDuCostDeDepart").GetString());
        Assert.Equal(4m, (await PlayerAsync(client, serie.TeamB[0])).GetProperty("currentCost").GetDecimal());
    }

    [Fact]
    public async Task Le_cost_fixe_par_un_admin_devient_le_cost_de_depart_du_mois_et_n_est_pas_ecrase()
    {
        var client = Factory.CreateSignedInClient();
        var admin = Factory.CreateAdminClient();
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        await ReadJsonAsync(await admin.PutAsJsonAsync($"/players/{serie.TeamA[0]}/cost", new { cost = 4m }, Ct));

        var fixe = await CostAsync(client, serie.TeamA[0]);
        Assert.Equal(4m, fixe.GetProperty("costDeDepart").GetDecimal());
        Assert.Equal("FixeParAdmin", fixe.GetProperty("sourceDuCostDeDepart").GetString());

        // Une game terminée ne ramène pas le cost au Rang SoloQ.
        await JouerUneGameNoteeAsync(client, serie.SeriesId, "B");
        Assert.Equal(4m, (await PlayerAsync(client, serie.TeamA[0])).GetProperty("currentCost").GetDecimal());

        // A1 noté 79, 0 victoire sur 5 : 4 + 0,406 − 0,3 = 4,106 → 4,1.
        var seriesId = serie.SeriesId;
        for (var i = 1; i < 5; i++)
        {
            if (i == 3) seriesId = await RevancheAsync(client, seriesId);
            await JouerUneGameNoteeAsync(client, seriesId, "B");
        }
        Assert.Equal(4.1m, (await CostAsync(client, serie.TeamA[0])).GetProperty("cost").GetDecimal());

        Factory.Clock.SetUtcNow(Avril);
        Assert.Equal(4.1m, (await CostAsync(client, serie.TeamA[0])).GetProperty("costDeDepart").GetDecimal());
    }

    [Fact]
    public async Task Deux_games_des_memes_joueurs_terminees_en_meme_temps_sont_toutes_les_deux_enregistrees()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 5);
        var autre = await NouvelleSerieAsync(client, serie);
        var premiere = await PlayGameAsync(client, serie.SeriesId);
        var seconde = await PlayGameAsync(client, autre);

        // Aucun mois n'est encore enregistré : les deux recalculs créent la même ligne (joueur, mois).
        var reponses = await Task.WhenAll(
            client.PutAsJsonAsync($"/games/{premiere}/result", new { winner = "A" }, Ct),
            client.PutAsJsonAsync($"/games/{seconde}/result", new { winner = "B" }, Ct));

        Assert.All(reponses, r => Assert.Equal(HttpStatusCode.OK, r.StatusCode));
        Assert.Single(await HistoriqueAsync(client, serie.TeamA[0]));
    }

    [Fact]
    public async Task Le_cost_d_un_joueur_inconnu_repond_404()
    {
        var client = Factory.CreateSignedInClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/players/999/cost", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/players/999/cost/historique", Ct)).StatusCode);
    }

    private async Task<JsonElement> CostAsync(HttpClient client, int playerId) =>
        await ReadJsonAsync(await client.GetAsync($"/players/{playerId}/cost", Ct));

    private async Task<JsonElement> PlayerAsync(HttpClient client, int playerId) =>
        await ReadJsonAsync(await client.GetAsync($"/players/{playerId}", Ct));

    private async Task<JsonElement> JoueurDeLaListeAsync(HttpClient client, int playerId) =>
        (await ReadJsonAsync(await client.GetAsync("/players", Ct))).EnumerateArray()
            .Single(p => p.GetProperty("id").GetInt32() == playerId);

    private async Task<List<JsonElement>> HistoriqueAsync(HttpClient client, int playerId) =>
        (await ReadJsonAsync(await client.GetAsync($"/players/{playerId}/cost/historique", Ct)))
            .GetProperty("mois").EnumerateArray().ToList();

    private async Task<int> RevancheAsync(HttpClient client, int seriesId) =>
        (await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{seriesId}/rematch",
            new { format = 3, fearless = false, alternateSides = false }, Ct))).GetProperty("seriesId").GetInt32();

    private int _rapports;

    /// <summary>
    /// Lance une game et la termine par un rapport du Companion sans durée : A1 (0/2/7, KP 70 %) y
    /// est noté 79 (ADR 0011), quelle que soit l'Équipe gagnante.
    /// </summary>
    private async Task JouerUneGameNoteeAsync(HttpClient client, int seriesId, string gagnante)
    {
        await PlayGameAsync(client, seriesId, teamASide: "blue");
        await EnvoyerUnRapportNoteAsync(client, seriesId, gagnante);
    }

    private async Task EnvoyerUnRapportNoteAsync(HttpClient client, int seriesId, string gagnante)
    {
        var code = await CodeCompanionAsync(client, seriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport($"EUW1_9571{_rapports++:D5}", "blue", gagnante,
            donnees: new { gameMode = "CLASSIC" })));
    }

    /// <summary>Une autre Série entre les mêmes Équipes, dans la Soirée ouverte (rouverte au besoin).</summary>
    private async Task<int> NouvelleSerieAsync(HttpClient client, CreatedSerie serie)
    {
        var open = await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct));
        if (open.GetProperty("soiree").ValueKind == JsonValueKind.Null)
            await OpenSoireeAsync(client);
        return (await ReadJsonAsync(await client.PostAsJsonAsync("/series",
            new { teamA = serie.TeamA, teamB = serie.TeamB, format = 5, fearless = false, alternateSides = false }, Ct)))
            .GetProperty("seriesId").GetInt32();
    }
}
