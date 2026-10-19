using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// La présence du Companion (ADR 0010) : avec le code de sa Série, il signale le PUUID du compte connecté
/// au client LoL ; l'Administration liste les Companions connectés (dernier signal de moins de 90 s).
/// La présence est éphémère, gardée en mémoire.
/// </summary>
public class PresenceCompanionTests(ApiFactory factory) : IntegrationTest(factory)
{
    private async Task<HttpResponseMessage> SignalerAsync(string code, string? puuid) =>
        await Factory.CreateAnonymousClient().PostAsJsonAsync($"/companion/{Uri.EscapeDataString(code)}/presence", new { puuid }, Ct);

    private async Task<JsonElement> ConnectesAsync() =>
        await ReadJsonAsync(await Factory.CreateAdminClient().GetAsync("/companion/connectes", Ct));

    [Fact]
    public async Task Sans_signal_aucun_companion_n_est_connecte()
    {
        Assert.Equal(0, (await ConnectesAsync()).GetArrayLength());
    }

    [Fact]
    public async Task Un_signal_fait_apparaitre_le_joueur_retrouve_par_son_puuid_avec_sa_serie_et_vu_il_y_a_x_secondes()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        var puuid = $"puuid-A1#EUW";

        var reponse = await SignalerAsync(code, puuid);
        Factory.Clock.Advance(TimeSpan.FromSeconds(12));

        Assert.Equal(HttpStatusCode.NoContent, reponse.StatusCode);
        var liste = await ConnectesAsync();
        var connecte = Assert.Single(liste.EnumerateArray());
        Assert.Equal(serie.SeriesId, connecte.GetProperty("seriesId").GetInt32());
        Assert.Equal(serie.TeamA[0], connecte.GetProperty("joueur").GetProperty("id").GetInt32());
        Assert.Equal("A1", connecte.GetProperty("joueur").GetProperty("prenom").GetString());
        Assert.Equal(12, connecte.GetProperty("vuIlYASecondes").GetInt32());
    }

    [Fact]
    public async Task Un_puuid_inconnu_est_un_joueur_inconnu_et_le_signal_reussit()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        var reponse = await SignalerAsync(code, "puuid-que-personne-ne-connait");

        Assert.Equal(HttpStatusCode.NoContent, reponse.StatusCode);
        var connecte = Assert.Single((await ConnectesAsync()).EnumerateArray());
        Assert.Equal(JsonValueKind.Null, connecte.GetProperty("joueur").ValueKind);
        Assert.Equal(serie.SeriesId, connecte.GetProperty("seriesId").GetInt32());
    }

    [Fact]
    public async Task Un_signal_renouvelle_le_vu_il_y_a_et_un_joueur_n_apparait_qu_une_fois()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await SignalerAsync(code, "puuid-A1#EUW");
        Factory.Clock.Advance(TimeSpan.FromSeconds(40));

        await SignalerAsync(code, "puuid-A1#EUW");
        Factory.Clock.Advance(TimeSpan.FromSeconds(5));

        var connecte = Assert.Single((await ConnectesAsync()).EnumerateArray());
        Assert.Equal(5, connecte.GetProperty("vuIlYASecondes").GetInt32());
    }

    [Fact]
    public async Task Un_companion_sans_signal_depuis_90_secondes_n_est_plus_connecte()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await SignalerAsync(code, "puuid-A1#EUW");

        Factory.Clock.Advance(TimeSpan.FromSeconds(89));
        Assert.Equal(1, (await ConnectesAsync()).GetArrayLength());

        Factory.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(0, (await ConnectesAsync()).GetArrayLength());
    }

    [Fact]
    public async Task Le_signal_est_refuse_pour_un_code_inconnu_renouvele_ou_de_serie_terminee()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var ancien = await CodeCompanionAsync(client, serie.SeriesId);
        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/code-companion/renouveler", null, Ct));
        var courant = await CodeCompanionAsync(client, serie.SeriesId);

        await AssertRefusAsync(await SignalerAsync("code-inconnu", "puuid-A1#EUW"), HttpStatusCode.NotFound);
        await AssertRefusAsync(await SignalerAsync(ancien, "puuid-A1#EUW"), HttpStatusCode.NotFound);

        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await AssertRefusAsync(await SignalerAsync(courant, "puuid-A1#EUW"), HttpStatusCode.NotFound);
        Assert.Equal(0, (await ConnectesAsync()).GetArrayLength());
    }

    [Fact]
    public async Task Un_signal_sans_puuid_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        Assert.Equal(HttpStatusCode.BadRequest, (await SignalerAsync(code, "")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SignalerAsync(code, null)).StatusCode);
        Assert.Equal(0, (await ConnectesAsync()).GetArrayLength());
    }

    [Fact]
    public async Task La_liste_des_companions_connectes_est_reservee_aux_administrateurs()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await Factory.CreateAnonymousClient().GetAsync("/companion/connectes", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Factory.CreateSignedInClient("7001", "Simple").GetAsync("/companion/connectes", Ct)).StatusCode);
    }
}
