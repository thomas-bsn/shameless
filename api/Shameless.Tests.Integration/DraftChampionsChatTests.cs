using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// Chat de la Draft de champions : les Capitaines (Lien capitaine) et les spectateurs (Lien spectateur)
/// y écrivent ; les messages se relisent tant que la draft n'est pas terminée.
/// </summary>
public class DraftChampionsChatTests(ApiFactory factory) : IntegrationTest(factory)
{
    private sealed record Preparee(int SeriesId, string LienA, string LienB, string LienSpectateur);

    private async Task<Preparee> OuvrirDraftAsync(HttpClient client)
    {
        var serie = await CreateSerieAsync(client);
        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{serie.SeriesId}/capitaines/A",
            new { designation = "Manuel", playerId = serie.TeamA[0] }, Ct));
        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{serie.SeriesId}/capitaines/B",
            new { designation = "Manuel", playerId = serie.TeamB[0] }, Ct));
        await PlayGameAsync(client, serie.SeriesId, "blue");
        var liens = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}/liens", Ct));
        (await client.PostAsync($"/series/{serie.SeriesId}/draft-champions", null, Ct)).EnsureSuccessStatusCode();
        return new Preparee(serie.SeriesId,
            liens.GetProperty("lienCapitaineA").GetString()!,
            liens.GetProperty("lienCapitaineB").GetString()!,
            liens.GetProperty("lienSpectateur").GetString()!);
    }

    private static string Url(string lien) => $"/liens/{Uri.EscapeDataString(lien)}/draft-champions/messages";

    private Task<HttpResponseMessage> EcrireAsync(string lien, string contenu, string? spectateurId = null, HttpClient? client = null) =>
        (client ?? Factory.CreateAnonymousClient()).PostAsJsonAsync(Url(lien), new { contenu, spectateurId }, Ct);

    private async Task<string?> Detail(HttpResponseMessage reponse) =>
        (await reponse.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("detail").GetString();

    private async Task<JsonElement[]> LireAsync(string lien) =>
        (await ReadJsonAsync(await Factory.CreateAnonymousClient().GetAsync(Url(lien), Ct)))
        .GetProperty("messages").EnumerateArray().ToArray();

    [Fact]
    public async Task Un_capitaine_et_un_spectateur_ecrivent_et_les_deux_pages_voient_les_messages()
    {
        var preparee = await OuvrirDraftAsync(Factory.CreateSignedInClient());

        (await EcrireAsync(preparee.LienA, "gl hf")).EnsureSuccessStatusCode();
        (await EcrireAsync(preparee.LienSpectateur, "allez B", "tok-1")).EnsureSuccessStatusCode();

        foreach (var lien in new[] { preparee.LienA, preparee.LienB, preparee.LienSpectateur })
        {
            var messages = await LireAsync(lien);
            Assert.Equal(["gl hf", "allez B"], messages.Select(m => m.GetProperty("contenu").GetString()));
            Assert.Equal("A1", messages[0].GetProperty("auteur").GetString());
            Assert.Equal("A", messages[0].GetProperty("equipe").GetString());
            Assert.Equal(JsonValueKind.Null, messages[1].GetProperty("equipe").ValueKind);
        }
    }

    [Fact]
    public async Task Un_spectateur_sans_connexion_a_un_pseudo_automatique_stable()
    {
        var preparee = await OuvrirDraftAsync(Factory.CreateSignedInClient());

        (await EcrireAsync(preparee.LienSpectateur, "un", "tok-1")).EnsureSuccessStatusCode();
        (await EcrireAsync(preparee.LienSpectateur, "deux", "tok-2")).EnsureSuccessStatusCode();
        Factory.Clock.Advance(TimeSpan.FromMinutes(1));
        (await EcrireAsync(preparee.LienSpectateur, "trois", "tok-1")).EnsureSuccessStatusCode();

        var messages = await LireAsync(preparee.LienSpectateur);
        Assert.Equal(["Spectateur 1", "Spectateur 2", "Spectateur 1"], messages.Select(m => m.GetProperty("auteur").GetString()));
    }

    [Fact]
    public async Task Un_spectateur_connecte_ecrit_sous_son_nom_discord()
    {
        var preparee = await OuvrirDraftAsync(Factory.CreateSignedInClient());
        var connecte = Factory.CreateSignedInClient("2002", "Sam");

        (await EcrireAsync(preparee.LienSpectateur, "salut", client: connecte)).EnsureSuccessStatusCode();

        Assert.Equal("Sam", (await LireAsync(preparee.LienSpectateur))[0].GetProperty("auteur").GetString());
    }

    [Fact]
    public async Task Un_message_vide_ou_trop_long_est_refuse_avec_un_message_lisible()
    {
        var preparee = await OuvrirDraftAsync(Factory.CreateSignedInClient());

        var vide = await EcrireAsync(preparee.LienA, "   ");
        var long_ = await EcrireAsync(preparee.LienA, new string('x', 301));

        Assert.Equal(HttpStatusCode.BadRequest, vide.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, long_.StatusCode);
        Assert.Contains("300", await Detail(long_));
        Assert.Empty(await LireAsync(preparee.LienA));
    }

    [Fact]
    public async Task Le_debit_est_limite_par_personne_puis_se_libere_avec_le_temps()
    {
        var preparee = await OuvrirDraftAsync(Factory.CreateSignedInClient());
        for (var i = 0; i < 5; i++)
            (await EcrireAsync(preparee.LienSpectateur, $"m{i}", "spam")).EnsureSuccessStatusCode();

        var refuse = await EcrireAsync(preparee.LienSpectateur, "m5", "spam");
        var autre = await EcrireAsync(preparee.LienSpectateur, "moi", "autre");

        Assert.Equal(HttpStatusCode.TooManyRequests, refuse.StatusCode);
        Assert.Contains("trop vite", await Detail(refuse));
        autre.EnsureSuccessStatusCode();

        Factory.Clock.Advance(TimeSpan.FromSeconds(11));
        (await EcrireAsync(preparee.LienSpectateur, "m5", "spam")).EnsureSuccessStatusCode();
    }

    private async Task<int> GameIdAsync(HttpClient client, int seriesId) =>
        (await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct))).GetProperty("games")[0].GetProperty("id").GetInt32();

    [Fact]
    public async Task Une_fois_la_draft_terminee_le_chat_est_vide_et_ferme()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);
        (await EcrireAsync(preparee.LienSpectateur, "avant la fin", "tok-1")).EnsureSuccessStatusCode();
        var gameId = await GameIdAsync(client, preparee.SeriesId);

        await JouerLaDraftAsync(client, preparee.SeriesId, gameId);

        Assert.Empty(await LireAsync(preparee.LienSpectateur));
        Assert.Empty(await LireAsync(preparee.LienA));
        var refuse = await EcrireAsync(preparee.LienSpectateur, "apres", "tok-1");
        Assert.Equal(HttpStatusCode.Conflict, refuse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await EcrireAsync(preparee.LienB, "apres")).StatusCode);
    }

    [Fact]
    public async Task Un_reset_accepte_efface_le_chat()
    {
        var client = Factory.CreateSignedInClient();
        var preparee = await OuvrirDraftAsync(client);
        (await EcrireAsync(preparee.LienSpectateur, "avant le reset", "tok-1")).EnsureSuccessStatusCode();
        (await EcrireAsync(preparee.LienA, "gl")).EnsureSuccessStatusCode();
        var anonyme = Factory.CreateAnonymousClient();

        (await anonyme.PostAsync($"/liens/{Uri.EscapeDataString(preparee.LienA)}/draft-champions/reset", null, Ct)).EnsureSuccessStatusCode();
        (await anonyme.PostAsJsonAsync($"/liens/{Uri.EscapeDataString(preparee.LienB)}/draft-champions/reset/reponse",
            new { accepte = true }, Ct)).EnsureSuccessStatusCode();

        (await client.PostAsync($"/series/{preparee.SeriesId}/draft-champions", null, Ct)).EnsureSuccessStatusCode();
        Assert.Empty(await LireAsync(preparee.LienSpectateur));
        Assert.Empty(await LireAsync(preparee.LienB));
    }

    [Fact]
    public async Task Des_spectateurs_qui_arrivent_en_meme_temps_ont_chacun_un_numero_different()
    {
        var preparee = await OuvrirDraftAsync(Factory.CreateSignedInClient());

        var reponses = await Task.WhenAll(Enumerable.Range(0, 8).Select(i =>
            EcrireAsync(preparee.LienSpectateur, $"m{i}", $"tok-{i}")));
        foreach (var r in reponses) r.EnsureSuccessStatusCode();

        var pseudos = (await LireAsync(preparee.LienSpectateur)).Select(m => m.GetProperty("auteur").GetString()).ToArray();
        Assert.Equal(8, pseudos.Distinct().Count());
    }

    [Fact]
    public async Task Un_lien_inconnu_est_refuse()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await EcrireAsync("inconnu", "x")).StatusCode);
    }
}
