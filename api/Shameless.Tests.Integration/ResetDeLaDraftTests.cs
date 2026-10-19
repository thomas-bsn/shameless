using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Reset de la draft : un Capitaine demande, l'autre accepte ou refuse, sous 60 s. Accepté, la draft
/// repart de zéro (même Game) et les deux Capitaines doivent se remettre prêts.
/// </summary>
public class ResetDeLaDraftTests(ApiFactory factory) : IntegrationTest(factory)
{
    private sealed record Serie(int Id, string LienA, string LienB, string LienSpectateur, int GameId);

    private async Task<Serie> DraftEnCoursAsync(HttpClient client)
    {
        var serie = await CreateSerieAsync(client);
        await DesignerLesCapitainesAbsentsAsync(client, serie.SeriesId);
        var liens = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}/liens", Ct));
        var a = liens.GetProperty("lienCapitaineA").GetString()!;
        var b = liens.GetProperty("lienCapitaineB").GetString()!;
        foreach (var lien in new[] { a, b })
        {
            (await Anonyme().GetAsync($"/liens/{Uri.EscapeDataString(lien)}", Ct)).EnsureSuccessStatusCode();
            (await Anonyme().PutAsJsonAsync($"/liens/{Uri.EscapeDataString(lien)}/pret", new { pret = true }, Ct)).EnsureSuccessStatusCode();
        }
        var gameId = (await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}", Ct)))
            .GetProperty("games")[0].GetProperty("id").GetInt32();
        return new Serie(serie.SeriesId, a, b, liens.GetProperty("lienSpectateur").GetString()!, gameId);
    }

    private HttpClient Anonyme() => Factory.CreateAnonymousClient();

    private Task<HttpResponseMessage> DemanderAsync(string lien) =>
        Anonyme().PostAsync($"/liens/{Uri.EscapeDataString(lien)}/draft-champions/reset", null, Ct);

    private Task<HttpResponseMessage> RepondreAsync(string lien, bool accepte) =>
        Anonyme().PostAsJsonAsync($"/liens/{Uri.EscapeDataString(lien)}/draft-champions/reset/reponse", new { accepte }, Ct);

    private Task<HttpResponseMessage> JouerAsync(string lien, int tour, string championId) =>
        Anonyme().PostAsJsonAsync($"/liens/{Uri.EscapeDataString(lien)}/draft-champions/actions", new { tour, championId }, Ct);

    private async Task<JsonElement> DraftAsync(string lien) =>
        await ReadJsonAsync(await Anonyme().GetAsync($"/liens/{Uri.EscapeDataString(lien)}/draft-champions", Ct));

    private async Task<JsonElement> LiensAsync(HttpClient client, int id) =>
        await ReadJsonAsync(await client.GetAsync($"/series/{id}/liens", Ct));

    private static string Premiere(JsonElement draft) => draft.GetProperty("tourCourant").GetProperty("equipe").GetString()!;

    [Fact]
    public async Task Accepte_la_draft_repart_de_zero_meme_game_et_les_deux_doivent_se_remettre_prets()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await DraftEnCoursAsync(client);
        var draft = await DraftAsync(serie.LienA);
        var equipe = Premiere(draft);
        await JouerAsync(equipe == "A" ? serie.LienA : serie.LienB, 1, "Ahri");

        (await DemanderAsync(serie.LienA)).EnsureSuccessStatusCode();
        var demande = (await DraftAsync(serie.LienB)).GetProperty("resetDemande");
        Assert.Equal("A", demande.GetProperty("equipe").GetString());

        (await RepondreAsync(serie.LienB, true)).EnsureSuccessStatusCode();

        await AssertRefusAsync(await Anonyme().GetAsync($"/liens/{Uri.EscapeDataString(serie.LienA)}/draft-champions", Ct), HttpStatusCode.NotFound);
        var liens = await LiensAsync(client, serie.Id);
        Assert.Equal(JsonValueKind.Null, liens.GetProperty("capitaineAPretLe").ValueKind);
        Assert.Equal(JsonValueKind.Null, liens.GetProperty("capitaineBPretLe").ValueKind);
        var games = (await ReadJsonAsync(await client.GetAsync($"/series/{serie.Id}", Ct))).GetProperty("games");
        Assert.Equal(1, games.GetArrayLength());

        await Anonyme().PutAsJsonAsync($"/liens/{Uri.EscapeDataString(serie.LienA)}/pret", new { pret = true }, Ct);
        await Anonyme().PutAsJsonAsync($"/liens/{Uri.EscapeDataString(serie.LienB)}/pret", new { pret = true }, Ct);
        var apres = await DraftAsync(serie.LienA);
        Assert.Equal(serie.GameId, apres.GetProperty("gameId").GetInt32());
        Assert.Equal(0, apres.GetProperty("actions").GetArrayLength());
        Assert.Equal(JsonValueKind.Null, apres.GetProperty("resetDemande").ValueKind);
        Assert.Equal(1, (await ReadJsonAsync(await client.GetAsync($"/series/{serie.Id}", Ct))).GetProperty("games").GetArrayLength());
    }

    [Fact]
    public async Task Refuse_rien_ne_change()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await DraftEnCoursAsync(client);
        await DemanderAsync(serie.LienA);

        (await RepondreAsync(serie.LienB, false)).EnsureSuccessStatusCode();

        var draft = await DraftAsync(serie.LienA);
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("resetDemande").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, (await LiensAsync(client, serie.Id)).GetProperty("capitaineAPretLe").ValueKind);
    }

    [Fact]
    public async Task Sans_reponse_sous_60_s_rien_ne_change_et_on_ne_peut_plus_repondre()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await DraftEnCoursAsync(client);
        await DemanderAsync(serie.LienA);

        Factory.Clock.Advance(TimeSpan.FromSeconds(59));
        Assert.Equal("A", (await DraftAsync(serie.LienB)).GetProperty("resetDemande").GetProperty("equipe").GetString());

        Factory.Clock.Advance(TimeSpan.FromSeconds(2));
        Assert.Equal(JsonValueKind.Null, (await DraftAsync(serie.LienB)).GetProperty("resetDemande").ValueKind);
        await AssertRefusAsync(await RepondreAsync(serie.LienB, true), HttpStatusCode.Conflict);
        Assert.Equal(serie.GameId, (await DraftAsync(serie.LienB)).GetProperty("gameId").GetInt32());
    }

    [Fact]
    public async Task Le_demandeur_ne_repond_pas_a_sa_propre_demande_et_une_demande_ne_se_double_pas()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await DraftEnCoursAsync(client);
        await AssertRefusAsync(await RepondreAsync(serie.LienA, true), HttpStatusCode.Conflict);
        await DemanderAsync(serie.LienA);

        await AssertRefusAsync(await RepondreAsync(serie.LienA, true), HttpStatusCode.Conflict);
        await AssertRefusAsync(await DemanderAsync(serie.LienB), HttpStatusCode.Conflict);
        await AssertRefusAsync(await DemanderAsync(serie.LienA), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Un_spectateur_ne_demande_ni_ne_repond()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await DraftEnCoursAsync(client);

        await AssertRefusAsync(await DemanderAsync(serie.LienSpectateur), HttpStatusCode.Forbidden);
        await DemanderAsync(serie.LienA);
        await AssertRefusAsync(await RepondreAsync(serie.LienSpectateur, true), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Refuse_une_fois_la_draft_terminee()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await DraftEnCoursAsync(client);
        var gameId = serie.GameId;
        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct));

        await AssertRefusAsync(await DemanderAsync(serie.LienA), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Ni_l_organisateur_ni_l_administrateur_n_ont_de_reset_direct()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await DraftEnCoursAsync(client);

        var reponse = await client.PostAsync($"/games/{serie.GameId}/draft-champions/reset", null, Ct);
        Assert.Contains(reponse.StatusCode, new[] { HttpStatusCode.NotFound, HttpStatusCode.MethodNotAllowed });
    }
}
