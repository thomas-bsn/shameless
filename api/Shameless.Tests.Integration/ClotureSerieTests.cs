using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// Quand la Série se termine (gagnée, nulle, interrompue ou annulée), son code Companion cesse de
/// fonctionner : il ne se résout plus et tout nouveau rapport est refusé. Les rapports déjà reçus
/// restent traités : le renvoi d'un rapport déjà rattaché est dédoublonné sans erreur.
/// </summary>
public class ClotureSerieTests(ApiFactory factory) : IntegrationTest(factory)
{
    private async Task<HttpResponseMessage> ResoudreAsync(string code) =>
        await Factory.CreateAnonymousClient().GetAsync($"/companion/{Uri.EscapeDataString(code)}", Ct);

    private async Task<JsonElement> SerieAsync(HttpClient client, int seriesId) =>
        await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));

    /// <summary>Mène une Série à sa clôture par saisie manuelle ou arrêt, et renvoie son code.</summary>
    private async Task<string> CloturerAsync(HttpClient client, string cloture)
    {
        var serie = await CreateSerieAsync(client, format: cloture == "nulle" ? 2 : 3);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        switch (cloture)
        {
            case "gagnee":
                await PlayAndWinAsync(client, serie.SeriesId, "A");
                await PlayAndWinAsync(client, serie.SeriesId, "A");
                break;
            case "nulle":
                await PlayAndWinAsync(client, serie.SeriesId, "A");
                await PlayAndWinAsync(client, serie.SeriesId, "B");
                break;
            case "interrompue":
                await PlayAndWinAsync(client, serie.SeriesId, "A");
                await PlayGameAsync(client, serie.SeriesId);
                await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));
                break;
            case "annulee":
                await PlayGameAsync(client, serie.SeriesId);
                await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));
                break;
        }
        return code;
    }

    [Theory]
    [InlineData("gagnee")]
    [InlineData("nulle")]
    [InlineData("interrompue")]
    [InlineData("annulee")]
    public async Task Apres_la_cloture_le_code_ne_se_resout_plus_et_un_nouveau_rapport_est_refuse(string cloture)
    {
        var client = Factory.CreateSignedInClient();
        var code = await CloturerAsync(client, cloture);

        await AssertRefusAsync(await ResoudreAsync(code), HttpStatusCode.NotFound);
        await AssertRefusAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_9000000001", teamASide: "blue", gagnante: "A")),
            HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Renvoyer_apres_la_victoire_le_rapport_qui_l_a_decidee_le_designe_sans_rien_modifier()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        var premier = await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_9000000002", teamASide: "blue", gagnante: "B")));
        Assert.Equal("Gagnee", premier.GetProperty("outcome").GetString());
        var avant = await SerieAsync(client, serie.SeriesId);

        var reponse = await EnvoyerRapportAsync(code, Rapport("EUW1_9000000002", teamASide: "blue", gagnante: "B"));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        Assert.Equal(premier.ToString(), (await ReadJsonAsync(reponse)).ToString());
        Assert.Equal(avant.ToString(), (await SerieAsync(client, serie.SeriesId)).ToString());
    }

    [Fact]
    public async Task Renvoyer_apres_l_interruption_le_rapport_d_une_game_terminee_le_designe_sans_rien_modifier()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_9000000003", teamASide: "blue", gagnante: "A")));
        await PlayGameAsync(client, serie.SeriesId, teamASide: "red");
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));
        var avant = await SerieAsync(client, serie.SeriesId);

        var reponse = await EnvoyerRapportAsync(code, Rapport("EUW1_9000000003", teamASide: "blue", gagnante: "A"));

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var renvoi = await ReadJsonAsync(reponse);
        Assert.Equal(gameId, renvoi.GetProperty("gameId").GetInt32());
        Assert.Equal("Accepte", renvoi.GetProperty("statut").GetString());
        Assert.Equal("Interrompue", renvoi.GetProperty("outcome").GetString());
        Assert.Equal(avant.ToString(), (await SerieAsync(client, serie.SeriesId)).ToString());
        await AssertRefusAsync(await ResoudreAsync(code), HttpStatusCode.NotFound);
    }

    /// <summary>
    /// Une revue ouverte bloque la saisie manuelle et la game suivante : la Série ne peut donc pas être
    /// gagnée ni nulle avant la décision de l'organisateur, seul l'arrêt la clôt, et il efface la revue
    /// avec la game en attente (ADR 0009, voir <see cref="RapportsEnRevueTests"/>). Un rapport en revue
    /// est donc toujours tranché avant la clôture ; une fois tranché, son renvoi est dédoublonné.
    /// </summary>
    [Fact]
    public async Task Un_rapport_en_revue_est_tranche_avant_la_cloture_et_son_renvoi_apres_est_dedoublonne()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await CreatePlayerAsync(client, "X1");
        var gameId = await PlayGameAsync(client, serie.SeriesId, teamASide: "blue");
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        var ecart = Rapport("EUW1_9000000004", teamASide: "blue", gagnante: "A", equipeA: ["A1", "A2", "A3", "A4", "X1"]);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, ecart));
        var rapportId = (await SerieAsync(client, serie.SeriesId)).GetProperty("games")[0]
            .GetProperty("rapportEnRevue").GetProperty("id").GetInt32();

        await AssertRefusAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "B" }, Ct),
            HttpStatusCode.Conflict);
        var accepte = await ReadJsonAsync(await client.PostAsync($"/rapports/{rapportId}/accepter", null, Ct));
        Assert.Equal("Gagnee", accepte.GetProperty("outcome").GetString());

        await AssertRefusAsync(await client.PostAsync($"/rapports/{rapportId}/rejeter", null, Ct), HttpStatusCode.Conflict);
        var renvoi = await EnvoyerRapportAsync(code, ecart);
        Assert.Equal(HttpStatusCode.OK, renvoi.StatusCode);
        Assert.Equal(accepte.ToString(), (await ReadJsonAsync(renvoi)).ToString());
        await AssertRefusAsync(await ResoudreAsync(code), HttpStatusCode.NotFound);
    }
}
