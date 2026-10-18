using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// L'échec d'un DM de Lien capitaine (compte Discord non lié, DM fermé) : le bot le remonte à l'API
/// (<c>/bot/series/{id}/liens-capitaine/{equipe}/dm-echec</c>, ADR 0013 et 0017) et l'organisateur le
/// lit avec ses liens, pour copier le lien à la main.
/// </summary>
public class EchecsDeDmTests(ApiFactory factory) : IntegrationTest(factory)
{
    private async Task<JsonElement> LiensAsync(HttpClient client, int seriesId) =>
        await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}/liens", Ct));

    private Task<HttpResponseMessage> SignalerAsync(int seriesId, string equipe, string raison) =>
        Factory.CreateBotClient().PostAsJsonAsync($"/bot/series/{seriesId}/liens-capitaine/{equipe}/dm-echec", new { raison }, Ct);

    private async Task DesignerAsync(HttpClient client, int seriesId, string equipe, int playerId) =>
        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{seriesId}/capitaines/{equipe}",
            new { designation = "Manuel", playerId }, Ct));

    [Fact]
    public async Task Sans_echec_signale_les_liens_n_ont_pas_d_alerte()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);

        var liens = await LiensAsync(client, serie.SeriesId);

        Assert.Equal(JsonValueKind.Null, liens.GetProperty("echecDmA").ValueKind);
        Assert.Equal(JsonValueKind.Null, liens.GetProperty("echecDmB").ValueKind);
    }

    [Theory]
    [InlineData("compte_non_lie")]
    [InlineData("dm_ferme")]
    [InlineData("autre")]
    public async Task Un_echec_de_dm_remonte_par_le_bot_est_lu_par_l_organisateur_pour_la_bonne_equipe(string raison)
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        await DesignerAsync(client, serie.SeriesId, "B", serie.TeamB[1]);

        var reponse = await SignalerAsync(serie.SeriesId, "B", raison);

        Assert.Equal(HttpStatusCode.NoContent, reponse.StatusCode);
        var liens = await LiensAsync(client, serie.SeriesId);
        Assert.Equal(raison, liens.GetProperty("echecDmB").GetProperty("raison").GetString());
        Assert.EndsWith("Z", liens.GetProperty("echecDmB").GetProperty("signaleLe").GetString());
        Assert.Equal(JsonValueKind.Null, liens.GetProperty("echecDmA").ValueKind);
    }

    [Fact]
    public async Task Renouveler_le_lien_efface_l_alerte_car_il_faut_alors_le_copier_a_la_main()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        await DesignerAsync(client, serie.SeriesId, "A", serie.TeamA[0]);
        await SignalerAsync(serie.SeriesId, "A", "dm_ferme");

        var liens = await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/liens/A/renouveler", null, Ct));

        Assert.Equal(JsonValueKind.Null, liens.GetProperty("echecDmA").ValueKind);
    }

    [Fact]
    public async Task Changer_de_capitaine_efface_l_alerte_du_precedent()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        await DesignerAsync(client, serie.SeriesId, "A", serie.TeamA[0]);
        await SignalerAsync(serie.SeriesId, "A", "compte_non_lie");

        await DesignerAsync(client, serie.SeriesId, "A", serie.TeamA[1]);

        Assert.Equal(JsonValueKind.Null, (await LiensAsync(client, serie.SeriesId)).GetProperty("echecDmA").ValueKind);
    }

    [Fact]
    public async Task Un_echec_est_refuse_pour_une_serie_inconnue_une_equipe_inconnue_ou_une_raison_inconnue()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        await DesignerAsync(client, serie.SeriesId, "A", serie.TeamA[0]);

        await AssertRefusAsync(await SignalerAsync(999, "A", "dm_ferme"), HttpStatusCode.NotFound);
        await AssertRefusAsync(await SignalerAsync(serie.SeriesId, "C", "dm_ferme"), HttpStatusCode.BadRequest);
        await AssertRefusAsync(await SignalerAsync(serie.SeriesId, "A", "inconnue"), HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Un_echec_pour_une_equipe_sans_capitaine_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);

        await AssertRefusAsync(await SignalerAsync(serie.SeriesId, "A", "dm_ferme"), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Seul_le_bot_signale_un_echec_de_dm()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        await DesignerAsync(client, serie.SeriesId, "A", serie.TeamA[0]);

        var reponse = await client.PostAsJsonAsync($"/bot/series/{serie.SeriesId}/liens-capitaine/A/dm-echec", new { raison = "dm_ferme" }, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, reponse.StatusCode);
    }
}
