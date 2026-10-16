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
    public async Task Sans_historique_le_cost_part_du_rang_soloq_et_n_evolue_qu_a_partir_de_cinq_games()
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

        // Games saisies à la main, sans note : 3 victoires sur 5 (60 %) → 0,3 × 20 % / 50 % = +0,06.
        var a1 = await CostAsync(client, serie.TeamA[0]);
        Assert.True(a1.GetProperty("evolue").GetBoolean());
        Assert.Equal(60, a1.GetProperty("winrate").GetDouble());
        Assert.Equal(JsonValueKind.Null, a1.GetProperty("noteMoyenne").ValueKind);
        Assert.Equal(2.1m, a1.GetProperty("cost").GetDecimal());
        Assert.Equal(2.1m, (await PlayerAsync(client, serie.TeamA[0])).GetProperty("currentCost").GetDecimal());
        Assert.Equal(1.9m, (await CostAsync(client, serie.TeamB[0])).GetProperty("cost").GetDecimal());
    }

    [Fact]
    public async Task Le_nouveau_mois_part_du_cost_final_du_mois_precedent_sans_effacer_son_historique()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        foreach (var gagnante in new[] { "A", "A", "B", "B", "A" })
            await PlayAndWinAsync(client, serie.SeriesId, gagnante);

        Factory.Clock.SetUtcNow(Avril);
        var debutAvril = await CostAsync(client, serie.TeamA[0]);
        Assert.Equal("2026-04-01", debutAvril.GetProperty("mois").GetString());
        Assert.Equal(2.1m, debutAvril.GetProperty("costDeDepart").GetDecimal());
        Assert.Equal("MoisPrecedent", debutAvril.GetProperty("sourceDuCostDeDepart").GetString());
        Assert.Equal(0, debutAvril.GetProperty("gamesTerminees").GetInt32());
        Assert.Equal(2.1m, debutAvril.GetProperty("cost").GetDecimal());

        // En avril, A gagne une game : moins de cinq games, le cost reste celui de départ.
        var revanche = await RevancheAsync(client, serie.SeriesId);
        await PlayAndWinAsync(client, revanche, "A");

        var historique = await HistoriqueAsync(client, serie.TeamA[0]);
        Assert.Equal(
            [("2026-03-01", 2m, 2.1m, 5, 3), ("2026-04-01", 2.1m, 2.1m, 1, 1)],
            historique.Select(m => (
                m.GetProperty("mois").GetString(),
                m.GetProperty("costDeDepart").GetDecimal(),
                m.GetProperty("cost").GetDecimal(),
                m.GetProperty("gamesTerminees").GetInt32(),
                m.GetProperty("victoires").GetInt32())));
    }

    [Fact]
    public async Task Le_cost_de_depart_est_le_dernier_cost_final_conserve_meme_apres_un_mois_sans_game()
    {
        var client = Factory.CreateSignedInClient();
        Factory.Clock.SetUtcNow(Mars);
        var serie = await CreateSerieAsync(client, format: 5);
        foreach (var gagnante in new[] { "B", "B", "A", "A", "B" })
            await PlayAndWinAsync(client, serie.SeriesId, gagnante);

        Factory.Clock.SetUtcNow(new DateTimeOffset(2026, 5, 12, 20, 0, 0, TimeSpan.Zero));

        var mai = await CostAsync(client, serie.TeamA[0]);
        Assert.Equal("2026-05-01", mai.GetProperty("mois").GetString());
        Assert.Equal(1.9m, mai.GetProperty("costDeDepart").GetDecimal());
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

        // 5 games terminées dont 3 de la Série interrompue ; A en gagne 3 → 2,1.
        var a1 = await CostAsync(client, serie.TeamA[0]);
        Assert.Equal(5, a1.GetProperty("gamesTerminees").GetInt32());
        Assert.Equal(3, a1.GetProperty("victoires").GetInt32());
        Assert.Equal(2.1m, a1.GetProperty("cost").GetDecimal());
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
            await PlayGameAsync(client, seriesId, teamASide: "blue");
            var code = await CodeCompanionAsync(client, seriesId);
            await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport($"EUW1_95700000{i}", "blue", "B",
                donnees: new { gameMode = "CLASSIC" })));
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

    private async Task<List<JsonElement>> HistoriqueAsync(HttpClient client, int playerId) =>
        (await ReadJsonAsync(await client.GetAsync($"/players/{playerId}/cost/historique", Ct)))
            .GetProperty("mois").EnumerateArray().ToList();

    private async Task<int> RevancheAsync(HttpClient client, int seriesId) =>
        (await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{seriesId}/rematch",
            new { format = 3, fearless = false, alternateSides = false }, Ct))).GetProperty("seriesId").GetInt32();
}
