using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Le code Companion d'une Série : un secret long, distinct des liens d'accès, qui ne sert qu'à
/// téléverser des rapports pour sa Série tant qu'elle est en cours.
/// </summary>
public class CodeCompanionTests(ApiFactory factory) : IntegrationTest(factory)
{
    private async Task<HttpResponseMessage> ResoudreAsync(string code) =>
        await Factory.CreateAnonymousClient().GetAsync($"/companion/{Uri.EscapeDataString(code)}", Ct);

    [Fact]
    public async Task Une_nouvelle_serie_a_un_code_companion_long_que_le_companion_resout_sans_connexion()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3, fearless: true);

        var code = await CodeCompanionAsync(client, serie.SeriesId);
        var resolu = await ResoudreAsync(code);

        Assert.True(code.Length >= 40, $"Code trop court : {code}");
        Assert.Equal(HttpStatusCode.OK, resolu.StatusCode);
        Assert.False(resolu.Headers.Contains("Set-Cookie"));
        var corps = await ReadJsonAsync(resolu);
        Assert.Equal(serie.SeriesId, corps.GetProperty("seriesId").GetInt32());
        Assert.Equal(3, corps.GetProperty("format").GetInt32());
        Assert.True(corps.GetProperty("fearless").GetBoolean());
        Assert.Equal(0, corps.GetProperty("teamAWins").GetInt32());
        Assert.Equal(["A1#EUW", "A2#EUW", "A3#EUW", "A4#EUW", "A5#EUW"],
            corps.GetProperty("equipeA").EnumerateArray().Select(j => j.GetString()).Order());
        Assert.Equal(["B1#EUW", "B2#EUW", "B3#EUW", "B4#EUW", "B5#EUW"],
            corps.GetProperty("equipeB").EnumerateArray().Select(j => j.GetString()).Order());
        Assert.DoesNotContain(code, corps.GetRawText());
    }

    [Fact]
    public async Task Chaque_serie_a_son_propre_code_distinct_de_ses_liens()
    {
        var client = Factory.CreateSignedInClient();
        var premiere = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, premiere.SeriesId, "A");
        var revanche = (await ReadJsonAsync(await client.PostAsJsonAsync(
            $"/series/{premiere.SeriesId}/rematch", new { format = 1 }, Ct))).GetProperty("seriesId").GetInt32();

        var code1 = await CodeCompanionAsync(client, premiere.SeriesId);
        var code2 = await CodeCompanionAsync(client, revanche);
        var liens = await ReadJsonAsync(await client.GetAsync($"/series/{revanche}/liens", Ct));

        Assert.NotEqual(code1, code2);
        Assert.DoesNotContain(code2, liens.GetRawText());
        Assert.Equal(revanche, (await ReadJsonAsync(await ResoudreAsync(code2))).GetProperty("seriesId").GetInt32());
    }

    [Fact]
    public async Task Renouveler_le_code_refuse_l_ancien_et_donne_un_nouveau_code_valable()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var ancien = await CodeCompanionAsync(client, serie.SeriesId);

        var renouvele = await ReadJsonAsync(await client.PostAsync(
            $"/series/{serie.SeriesId}/code-companion/renouveler", null, Ct));
        var nouveau = renouvele.GetProperty("code").GetString()!;

        Assert.NotEqual(ancien, nouveau);
        Assert.Equal(nouveau, await CodeCompanionAsync(client, serie.SeriesId));
        await AssertRefusAsync(await ResoudreAsync(ancien), HttpStatusCode.NotFound);
        Assert.Equal(HttpStatusCode.OK, (await ResoudreAsync(nouveau)).StatusCode);
    }

    [Fact]
    public async Task Un_code_inconnu_est_refuse()
    {
        await AssertRefusAsync(await ResoudreAsync("pas-un-vrai-code-pas-un-vrai-code-pas-un-vrai"), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Le_code_d_une_serie_gagnee_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        await PlayAndWinAsync(client, serie.SeriesId, "A");

        await AssertRefusAsync(await ResoudreAsync(code), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Le_code_d_une_serie_interrompue_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        await AssertRefusAsync(await ResoudreAsync(code), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Le_code_d_une_serie_annulee_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var code = await CodeCompanionAsync(client, serie.SeriesId);

        await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/stop", null, Ct));

        await AssertRefusAsync(await ResoudreAsync(code), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Le_code_ne_vaut_pas_lien_et_un_lien_ne_vaut_pas_code()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        var liens = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}/liens", Ct));
        var anonyme = Factory.CreateAnonymousClient();

        var commeLien = await anonyme.GetAsync($"/liens/{Uri.EscapeDataString(code)}", Ct);
        var draftParCode = await anonyme.GetAsync($"/liens/{Uri.EscapeDataString(code)}/draft-champions", Ct);
        var jouerParCode = await anonyme.PostAsJsonAsync($"/liens/{Uri.EscapeDataString(code)}/draft-champions/actions",
            new { numero = 1, championId = "Ahri" }, Ct);

        await AssertRefusAsync(commeLien, HttpStatusCode.NotFound);
        await AssertRefusAsync(draftParCode, HttpStatusCode.NotFound);
        await AssertRefusAsync(jouerParCode, HttpStatusCode.NotFound);
        foreach (var nom in new[] { "lienCapitaineA", "lienCapitaineB", "lienSpectateur" })
            await AssertRefusAsync(await ResoudreAsync(liens.GetProperty(nom).GetString()!), HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Le_code_n_ouvre_aucune_commande_d_organisateur()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var code = await CodeCompanionAsync(client, serie.SeriesId);
        var companion = Factory.CreateAnonymousClient();
        companion.DefaultRequestHeaders.Authorization = new("Bearer", code);

        Assert.Equal(HttpStatusCode.Unauthorized, (await companion.GetAsync($"/series/{serie.SeriesId}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await companion.GetAsync($"/series/{serie.SeriesId}/code-companion?code={code}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await companion.PostAsync($"/series/{serie.SeriesId}/games", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await companion.PostAsync($"/series/{serie.SeriesId}/code-companion/renouveler", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task Renouveler_le_code_d_une_serie_inconnue_ou_terminee_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        var inconnue = await client.PostAsync("/series/999/code-companion/renouveler", null, Ct);
        var terminee = await client.PostAsync($"/series/{serie.SeriesId}/code-companion/renouveler", null, Ct);

        Assert.Equal(HttpStatusCode.NotFound, inconnue.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/series/999/code-companion", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, terminee.StatusCode);
    }

}
