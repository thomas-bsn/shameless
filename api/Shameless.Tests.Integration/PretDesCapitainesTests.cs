using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;
using static Shameless.Tests.Integration.Infrastructure.RapportsDeTest;

namespace Shameless.Tests.Integration;

/// <summary>
/// « Prêt » des Capitaines : chacun se met prêt après avoir rejoint, et quand les deux le sont la Game est
/// créée et la Draft de champions démarre sans autre clic.
/// </summary>
public class PretDesCapitainesTests(ApiFactory factory) : IntegrationTest(factory)
{
    private sealed record Serie(int Id, string LienA, string LienB, string LienSpectateur);

    private async Task<Serie> PreparerAsync(HttpClient client, int format = 3)
    {
        var serie = await CreateSerieAsync(client, format);
        await DesignerLesCapitainesAbsentsAsync(client, serie.SeriesId);
        var liens = await ReadJsonAsync(await client.GetAsync($"/series/{serie.SeriesId}/liens", Ct));
        return new Serie(serie.SeriesId,
            liens.GetProperty("lienCapitaineA").GetString()!,
            liens.GetProperty("lienCapitaineB").GetString()!,
            liens.GetProperty("lienSpectateur").GetString()!);
    }

    private Task<HttpResponseMessage> RejoindreAsync(string lien) =>
        Factory.CreateAnonymousClient().GetAsync($"/liens/{Uri.EscapeDataString(lien)}", Ct);

    private Task<HttpResponseMessage> SeMettrePretAsync(string lien, bool pret = true) =>
        Factory.CreateAnonymousClient().PutAsJsonAsync($"/liens/{Uri.EscapeDataString(lien)}/pret", new { pret }, Ct);

    private async Task<JsonElement> SerieAsync(HttpClient client, int id) =>
        await ReadJsonAsync(await client.GetAsync($"/series/{id}", Ct));

    private async Task<JsonElement> LiensAsync(HttpClient client, int id) =>
        await ReadJsonAsync(await client.GetAsync($"/series/{id}/liens", Ct));

    [Fact]
    public async Task Un_capitaine_ne_peut_etre_pret_qu_apres_avoir_rejoint()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await PreparerAsync(client);

        await AssertRefusAsync(await SeMettrePretAsync(serie.LienA), HttpStatusCode.Conflict);

        (await RejoindreAsync(serie.LienA)).EnsureSuccessStatusCode();
        (await SeMettrePretAsync(serie.LienA)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Le_lien_spectateur_ne_met_personne_pret()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await PreparerAsync(client);

        await AssertRefusAsync(await SeMettrePretAsync(serie.LienSpectateur), HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Un_seul_capitaine_pret_ne_cree_pas_la_game_et_l_organisateur_voit_rejoint_et_pret()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await PreparerAsync(client);
        await RejoindreAsync(serie.LienA);
        await RejoindreAsync(serie.LienB);

        var reponse = await ReadJsonAsync(await SeMettrePretAsync(serie.LienA));

        Assert.True(reponse.GetProperty("pret").GetBoolean());
        Assert.False(reponse.GetProperty("adversairePret").GetBoolean());
        Assert.Equal(0, (await SerieAsync(client, serie.Id)).GetProperty("games").GetArrayLength());
        var liens = await LiensAsync(client, serie.Id);
        Assert.NotEqual(JsonValueKind.Null, liens.GetProperty("capitaineARejointLe").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, liens.GetProperty("capitaineAPretLe").ValueKind);
        Assert.Equal(JsonValueKind.Null, liens.GetProperty("capitaineBPretLe").ValueKind);
    }

    [Fact]
    public async Task Un_capitaine_se_remet_pas_pret_tant_que_l_autre_ne_l_est_pas_et_l_adversaire_le_voit()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await PreparerAsync(client);
        await RejoindreAsync(serie.LienA);
        await RejoindreAsync(serie.LienB);
        await SeMettrePretAsync(serie.LienA);

        var vuParB = await ReadJsonAsync(await RejoindreAsync(serie.LienB));
        Assert.True(vuParB.GetProperty("adversairePret").GetBoolean());

        var annule = await ReadJsonAsync(await SeMettrePretAsync(serie.LienA, pret: false));
        Assert.False(annule.GetProperty("pret").GetBoolean());
        Assert.Equal(JsonValueKind.Null, (await LiensAsync(client, serie.Id)).GetProperty("capitaineAPretLe").ValueKind);
    }

    [Fact]
    public async Task Quand_les_deux_sont_prets_la_game_est_creee_et_la_draft_demarre_sans_autre_clic()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await PreparerAsync(client);
        await RejoindreAsync(serie.LienA);
        await RejoindreAsync(serie.LienB);

        await SeMettrePretAsync(serie.LienA);
        Factory.Random.ImposerLeProchainTirage(0);
        (await SeMettrePretAsync(serie.LienB)).EnsureSuccessStatusCode();
        Factory.Random.LibererLeTirage();

        var games = (await SerieAsync(client, serie.Id)).GetProperty("games");
        Assert.Equal(1, games.GetArrayLength());
        Assert.Equal("blue", games[0].GetProperty("teamASide").GetString());
        var draft = await ReadJsonAsync(await Factory.CreateAnonymousClient()
            .GetAsync($"/liens/{Uri.EscapeDataString(serie.LienA)}/draft-champions", Ct));
        Assert.False(draft.GetProperty("terminee").GetBoolean());
        Assert.Equal(games[0].GetProperty("id").GetInt32(), draft.GetProperty("gameId").GetInt32());
    }

    [Fact]
    public async Task Apres_le_demarrage_on_ne_cree_pas_une_seconde_game_et_on_ne_peut_plus_annuler()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await PreparerAsync(client);
        await RejoindreAsync(serie.LienA);
        await RejoindreAsync(serie.LienB);
        await SeMettrePretAsync(serie.LienA);
        await SeMettrePretAsync(serie.LienB);

        await AssertRefusAsync(await SeMettrePretAsync(serie.LienA), HttpStatusCode.Conflict);
        await AssertRefusAsync(await SeMettrePretAsync(serie.LienA, pret: false), HttpStatusCode.Conflict);

        Assert.Equal(1, (await SerieAsync(client, serie.Id)).GetProperty("games").GetArrayLength());
    }

    [Fact]
    public async Task Changer_de_capitaine_remet_pas_pret_celui_qui_l_etait()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await PreparerAsync(client);
        await RejoindreAsync(serie.LienA);
        await SeMettrePretAsync(serie.LienA);
        var equipe = (await SerieAsync(client, serie.Id)).GetProperty("teamA");

        await ReadJsonAsync(await client.PutAsJsonAsync($"/series/{serie.Id}/capitaines/A",
            new { designation = "Manuel", playerId = equipe[1].GetProperty("playerId").GetInt32() }, Ct));

        Assert.Equal(JsonValueKind.Null, (await LiensAsync(client, serie.Id)).GetProperty("capitaineAPretLe").ValueKind);
    }

    [Fact]
    public async Task Le_pret_d_une_game_se_remet_a_zero_quand_elle_est_terminee_pour_la_suivante()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await PreparerAsync(client);
        await RejoindreAsync(serie.LienA);
        await RejoindreAsync(serie.LienB);
        await SeMettrePretAsync(serie.LienA);
        await SeMettrePretAsync(serie.LienB);
        var gameId = (await SerieAsync(client, serie.Id)).GetProperty("games")[0].GetProperty("id").GetInt32();

        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct));

        var liens = await LiensAsync(client, serie.Id);
        Assert.Equal(JsonValueKind.Null, liens.GetProperty("capitaineAPretLe").ValueKind);
        Assert.Equal(JsonValueKind.Null, liens.GetProperty("capitaineBPretLe").ValueKind);
        Assert.NotEqual(JsonValueKind.Null, liens.GetProperty("capitaineARejointLe").ValueKind);
    }

    [Fact]
    public async Task Une_game_dont_le_rapport_est_en_revue_n_ouvre_pas_de_draft_quand_les_deux_sont_prets()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await PreparerAsync(client);
        await CreatePlayerAsync(client, "X1");
        await RejoindreAsync(serie.LienA);
        await RejoindreAsync(serie.LienB);
        await SeMettrePretAsync(serie.LienA);
        await SeMettrePretAsync(serie.LienB);
        // Un Reset accepté laisse la game en attente, sans draft ; le Companion en rapporte alors une composition inconnue.
        var anonyme = Factory.CreateAnonymousClient();
        (await anonyme.PostAsync($"/liens/{Uri.EscapeDataString(serie.LienA)}/draft-champions/reset", null, Ct)).EnsureSuccessStatusCode();
        (await anonyme.PostAsJsonAsync($"/liens/{Uri.EscapeDataString(serie.LienB)}/draft-champions/reset/reponse",
            new { accepte = true }, Ct)).EnsureSuccessStatusCode();
        var code = await CodeCompanionAsync(client, serie.Id);
        await ReadJsonAsync(await EnvoyerRapportAsync(code, Rapport("EUW1_9100000001", teamASide: "blue", gagnante: "A",
            equipeA: ["A1", "A2", "A3", "A4", "X1"])));

        await SeMettrePretAsync(serie.LienA);
        await AssertRefusAsync(await SeMettrePretAsync(serie.LienB), HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Apres_une_game_terminee_les_capitaines_se_remettent_prets_et_la_game_suivante_s_ouvre_avec_le_side_inverse()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await PreparerAsync(client);
        await RejoindreAsync(serie.LienA);
        await RejoindreAsync(serie.LienB);
        await SeMettrePretAsync(serie.LienA);
        Factory.Random.ImposerLeProchainTirage(0);
        await SeMettrePretAsync(serie.LienB);
        Factory.Random.LibererLeTirage();
        var premiere = (await SerieAsync(client, serie.Id)).GetProperty("games")[0].GetProperty("id").GetInt32();
        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{premiere}/result", new { winner = "A" }, Ct));

        await SeMettrePretAsync(serie.LienA);
        var reponse = await ReadJsonAsync(await SeMettrePretAsync(serie.LienB));

        Assert.True(reponse.GetProperty("pret").GetBoolean());
        var games = (await SerieAsync(client, serie.Id)).GetProperty("games");
        Assert.Equal(2, games.GetArrayLength());
        Assert.Equal("red", games[1].GetProperty("teamASide").GetString());
        var draft = await ReadJsonAsync(await Factory.CreateAnonymousClient().GetAsync($"/liens/{Uri.EscapeDataString(serie.LienA)}/draft-champions", Ct));
        Assert.Equal(games[1].GetProperty("id").GetInt32(), draft.GetProperty("gameId").GetInt32());
        Assert.False(draft.GetProperty("terminee").GetBoolean());
    }

    [Fact]
    public async Task Le_lien_dit_si_le_pret_est_possible_tant_que_la_serie_est_en_cours_et_qu_aucune_draft_n_est_ouverte()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await PreparerAsync(client, format: 1);
        await RejoindreAsync(serie.LienA);
        await RejoindreAsync(serie.LienB);
        Task<bool> PretPossibleAsync() => Factory.CreateAnonymousClient()
            .GetAsync($"/liens/{Uri.EscapeDataString(serie.LienA)}", Ct)
            .ContinueWith(async r => (await ReadJsonAsync(await r)).GetProperty("pretPossible").GetBoolean()).Unwrap();

        Assert.True(await PretPossibleAsync());

        await SeMettrePretAsync(serie.LienA);
        await SeMettrePretAsync(serie.LienB);
        Assert.False(await PretPossibleAsync());

        var gameId = (await SerieAsync(client, serie.Id)).GetProperty("games")[0].GetProperty("id").GetInt32();
        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct));
        Assert.False(await PretPossibleAsync()); // BO1 gagné : la Série est terminée
    }

    [Fact]
    public async Task Apres_une_game_d_une_serie_encore_en_cours_le_pret_redevient_possible()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await PreparerAsync(client);
        await RejoindreAsync(serie.LienA);
        await RejoindreAsync(serie.LienB);
        await SeMettrePretAsync(serie.LienA);
        await SeMettrePretAsync(serie.LienB);
        var gameId = (await SerieAsync(client, serie.Id)).GetProperty("games")[0].GetProperty("id").GetInt32();
        await ReadJsonAsync(await client.PutAsJsonAsync($"/games/{gameId}/result", new { winner = "A" }, Ct));

        var acces = await ReadJsonAsync(await RejoindreAsync(serie.LienB));

        Assert.True(acces.GetProperty("pretPossible").GetBoolean());
        Assert.False(acces.GetProperty("pret").GetBoolean());
    }
}
