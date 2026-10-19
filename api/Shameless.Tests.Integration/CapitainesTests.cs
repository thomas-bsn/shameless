using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class CapitainesTests(ApiFactory factory) : IntegrationTest(factory)
{
    private static int? Capitaine(JsonElement serie, string equipe)
    {
        var capitaine = serie.GetProperty($"capitaine{equipe}");
        return capitaine.ValueKind == JsonValueKind.Null ? null : capitaine.GetInt32();
    }

    private async Task<JsonElement> SerieAsync(HttpClient client, int seriesId) =>
        await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));

    private async Task<JsonElement> LiensAsync(HttpClient client, int seriesId) =>
        await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}/liens", Ct));

    private static string Lien(JsonElement liens, string nom) => liens.GetProperty(nom).GetString()!;

    private async Task<HttpResponseMessage> ResoudreAsync(string lien) =>
        await Factory.CreateAnonymousClient().GetAsync($"/liens/{Uri.EscapeDataString(lien)}", Ct);

    [Fact]
    public async Task Une_serie_creee_depuis_une_draft_garde_ses_deux_capitaines()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var dix = await CreatePlayersAsync(client, "J1", "J2", "J3", "J4", "J5", "J6", "J7", "J8", "J9", "J10");
        await ReadJsonAsync(await client.PutAsJsonAsync("/soirees/open/picker/selection", new { playerIds = dix }, Ct));
        await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/draft",
            new { capitaines = "Manuels", capitaineA = dix[3], capitaineB = dix[7], premierPick = "A" }, Ct));
        var libres = dix.Where(id => id != dix[3] && id != dix[7]).ToArray();
        for (var i = 0; i < libres.Length; i++)
            await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/draft/picks",
                new { equipe = i % 2 == 0 ? "A" : "B", playerId = libres[i] }, Ct));

        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/validate", new { format = 3 }, Ct));

        var serie = await SerieAsync(client, created.GetProperty("seriesId").GetInt32());
        Assert.Equal(dix[3], Capitaine(serie, "A"));
        Assert.Equal(dix[7], Capitaine(serie, "B"));
    }

    private async Task<(int[] Dix, JsonElement Picker)> RouletteAsync(HttpClient client)
    {
        await OpenSoireeAsync(client);
        var dix = await CreatePlayersAsync(client, "J1", "J2", "J3", "J4", "J5", "J6", "J7", "J8", "J9", "J10");
        await ReadJsonAsync(await client.PutAsJsonAsync("/soirees/open/picker/selection", new { playerIds = dix }, Ct));
        var picker = await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/roulette",
            new { variante = "Aleatoire" }, Ct));
        return (dix, picker);
    }

    private static int[] Equipe(JsonElement picker, string nom) =>
        picker.GetProperty($"team{nom}").EnumerateArray().Select(p => p.GetProperty("playerId").GetInt32()).ToArray();

    [Fact]
    public async Task Une_serie_creee_apres_une_roulette_garde_les_capitaines_designes_au_picker()
    {
        var client = Factory.CreateSignedInClient();
        var (_, picker) = await RouletteAsync(client);
        var (a, b) = (Equipe(picker, "A")[2], Equipe(picker, "B")[4]);

        var created = await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/validate",
            new { format = 3, capitaineA = a, capitaineB = b }, Ct));

        var serie = await SerieAsync(client, created.GetProperty("seriesId").GetInt32());
        Assert.Equal(a, Capitaine(serie, "A"));
        Assert.Equal(b, Capitaine(serie, "B"));
    }

    [Fact]
    public async Task Valider_apres_une_roulette_refuse_un_capitaine_hors_de_son_equipe_ou_le_meme_des_deux_cotes()
    {
        var client = Factory.CreateSignedInClient();
        var (_, picker) = await RouletteAsync(client);
        var (equipeA, equipeB) = (Equipe(picker, "A"), Equipe(picker, "B"));

        var horsEquipe = await client.PostAsJsonAsync("/soirees/open/picker/validate",
            new { format = 3, capitaineA = equipeB[0], capitaineB = equipeA[0] }, Ct);
        var doublon = await client.PostAsJsonAsync("/soirees/open/picker/validate",
            new { format = 3, capitaineA = equipeA[0], capitaineB = equipeA[0] }, Ct);
        var incomplet = await client.PostAsJsonAsync("/soirees/open/picker/validate",
            new { format = 3, capitaineA = equipeA[0] }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, horsEquipe.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, doublon.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, incomplet.StatusCode);
    }

    private async Task<HttpResponseMessage> DesignerAsync(HttpClient client, int seriesId, string equipe, object body) =>
        await client.PutAsJsonAsync($"/series/{seriesId}/capitaines/{equipe}", body, Ct);

    [Fact]
    public async Task Une_serie_sans_draft_n_a_pas_de_capitaine_et_l_organisateur_les_designe_a_la_main()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        Assert.Null(Capitaine(await SerieAsync(client, serie.SeriesId), "A"));

        var designee = await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel", playerId = serie.TeamA[2] }));
        await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "B", new { designation = "Manuel", playerId = serie.TeamB[4] }));

        Assert.Equal(serie.TeamA[2], Capitaine(designee, "A"));
        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Equal(serie.TeamA[2], Capitaine(lue, "A"));
        Assert.Equal(serie.TeamB[4], Capitaine(lue, "B"));
    }

    [Fact]
    public async Task Designer_un_autre_capitaine_remplace_le_precedent()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel", playerId = serie.TeamA[0] }));

        var lue = await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel", playerId = serie.TeamA[1] }));

        Assert.Equal(serie.TeamA[1], Capitaine(lue, "A"));
        Assert.Null(Capitaine(lue, "B"));
    }

    [Fact]
    public async Task Le_capitaine_fait_partie_de_son_equipe()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);

        var autreEquipe = await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel", playerId = serie.TeamB[0] });
        var sansJoueur = await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel" });
        var equipeInconnue = await DesignerAsync(client, serie.SeriesId, "C", new { designation = "Manuel", playerId = serie.TeamA[0] });

        Assert.Equal(HttpStatusCode.BadRequest, autreEquipe.StatusCode);
        Assert.Equal("application/problem+json", autreEquipe.Content.Headers.ContentType?.MediaType);
        Assert.Equal(HttpStatusCode.BadRequest, sansJoueur.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, equipeInconnue.StatusCode);
        Assert.Null(Capitaine(await SerieAsync(client, serie.SeriesId), "A"));
    }

    [Fact]
    public async Task Le_capitaine_d_une_equipe_ne_peut_pas_etre_celui_de_l_autre_equipe()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "B", new { designation = "Manuel", playerId = serie.TeamB[0] }));

        var response = await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel", playerId = serie.TeamB[0] });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var lue = await SerieAsync(client, serie.SeriesId);
        Assert.Null(Capitaine(lue, "A"));
        Assert.Equal(serie.TeamB[0], Capitaine(lue, "B"));
    }

    [Fact]
    public async Task Designer_le_capitaine_d_une_serie_inconnue_repond_404()
    {
        var client = Factory.CreateSignedInClient();

        var response = await DesignerAsync(client, 999, "A", new { designation = "AuHasard" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Le_capitaine_designe_au_hasard_est_un_joueur_de_son_equipe()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);

        var tires = new HashSet<int>();
        for (var i = 0; i < 20; i++)
        {
            var lue = await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "B", new { designation = "AuHasard" }));
            tires.Add(Capitaine(lue, "B")!.Value);
        }

        Assert.Subset(serie.TeamB.ToHashSet(), tires);
        Assert.True(tires.Count > 1, "Le hasard désigne toujours le même joueur.");
    }

    [Fact]
    public async Task Le_capitaine_designe_par_cost_est_le_plus_gros_cost_de_son_equipe()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var admin = Factory.CreateAdminClient();
        // Le plus gros cost de la Série est dans l'Équipe B : il ne compte pas pour l'Équipe A.
        (await admin.PutAsJsonAsync($"/players/{serie.TeamB[0]}/cost", new { cost = 6 }, Ct)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/players/{serie.TeamA[3]}/cost", new { cost = 5 }, Ct)).EnsureSuccessStatusCode();
        (await admin.PutAsJsonAsync($"/players/{serie.TeamA[1]}/cost", new { cost = 4 }, Ct)).EnsureSuccessStatusCode();

        var lue = await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "A", new { designation = "PlusGrosCost" }));

        Assert.Equal(serie.TeamA[3], Capitaine(lue, "A"));
    }

    [Fact]
    public async Task Une_serie_terminee_ne_change_plus_de_capitaine()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        var response = await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel", playerId = serie.TeamA[0] });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Le_capitaine_se_designe_aussi_pendant_qu_une_game_attend_son_resultat()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        await PlayGameAsync(client, serie.SeriesId);

        var response = await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel", playerId = serie.TeamA[0] });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Le_remplacement_du_capitaine_laisse_son_equipe_sans_capitaine_jusqu_a_une_nouvelle_designation()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var remplacant = await CreatePlayerAsync(client, "R1");
        await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel", playerId = serie.TeamA[0] }));
        await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "B", new { designation = "Manuel", playerId = serie.TeamB[0] }));

        var apres = await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{serie.SeriesId}/replacements",
            new { replacedPlayerId = serie.TeamA[0], replacementPlayerId = remplacant }, Ct));

        Assert.Null(Capitaine(apres, "A"));
        Assert.Equal(serie.TeamB[0], Capitaine(apres, "B"));
        var redesignee = await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel", playerId = remplacant }));
        Assert.Equal(remplacant, Capitaine(redesignee, "A"));
    }

    [Fact]
    public async Task Le_remplacement_du_capitaine_renouvelle_le_lien_capitaine_de_son_equipe()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var remplacant = await CreatePlayerAsync(client, "R1");
        await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel", playerId = serie.TeamA[0] }));
        var avant = await LiensAsync(client, serie.SeriesId);

        await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{serie.SeriesId}/replacements",
            new { replacedPlayerId = serie.TeamA[0], replacementPlayerId = remplacant }, Ct));

        var apres = await LiensAsync(client, serie.SeriesId);
        Assert.NotEqual(Lien(avant, "lienCapitaineA"), Lien(apres, "lienCapitaineA"));
        Assert.Equal(Lien(avant, "lienCapitaineB"), Lien(apres, "lienCapitaineB"));
        Assert.Equal(HttpStatusCode.NotFound, (await ResoudreAsync(Lien(avant, "lienCapitaineA"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await ResoudreAsync(Lien(apres, "lienCapitaineA"))).StatusCode);
    }

    [Fact]
    public async Task Le_remplacement_d_un_autre_joueur_garde_le_lien_capitaine()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var remplacant = await CreatePlayerAsync(client, "R1");
        await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel", playerId = serie.TeamA[0] }));
        var avant = await LiensAsync(client, serie.SeriesId);

        await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{serie.SeriesId}/replacements",
            new { replacedPlayerId = serie.TeamA[1], replacementPlayerId = remplacant }, Ct));

        Assert.Equal(Lien(avant, "lienCapitaineA"), Lien(await LiensAsync(client, serie.SeriesId), "lienCapitaineA"));
    }

    [Fact]
    public async Task Le_remplacement_d_un_autre_joueur_garde_le_capitaine()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var remplacant = await CreatePlayerAsync(client, "R1");
        await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel", playerId = serie.TeamA[0] }));

        var apres = await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{serie.SeriesId}/replacements",
            new { replacedPlayerId = serie.TeamA[1], replacementPlayerId = remplacant }, Ct));

        Assert.Equal(serie.TeamA[0], Capitaine(apres, "A"));
    }

    [Fact]
    public async Task La_revanche_n_a_pas_de_capitaine_et_l_organisateur_les_designe()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        await ReadJsonAsync(await DesignerAsync(client, serie.SeriesId, "A", new { designation = "Manuel", playerId = serie.TeamA[0] }));
        await PlayAndWinAsync(client, serie.SeriesId, "A");

        var revanche = await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{serie.SeriesId}/rematch", new { format = 1 }, Ct));
        var revancheId = revanche.GetProperty("seriesId").GetInt32();

        Assert.Null(Capitaine(await SerieAsync(client, revancheId), "A"));
        var lue = await ReadJsonAsync(await DesignerAsync(client, revancheId, "A", new { designation = "Manuel", playerId = serie.TeamA[1] }));
        Assert.Equal(serie.TeamA[1], Capitaine(lue, "A"));
    }

    [Fact]
    public async Task Un_visiteur_non_connecte_ne_designe_pas_de_capitaine()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);

        var response = await DesignerAsync(Factory.CreateAnonymousClient(), serie.SeriesId, "A",
            new { designation = "Manuel", playerId = serie.TeamA[0] });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Null(Capitaine(await SerieAsync(client, serie.SeriesId), "A"));
    }
}
