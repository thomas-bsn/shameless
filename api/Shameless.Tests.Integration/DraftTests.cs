using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class DraftTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const string Selection = "/soirees/open/picker/selection";
    private const string DraftUrl = "/soirees/open/picker/draft";
    private const string Picks = "/soirees/open/picker/draft/picks";
    private const string Validation = "/soirees/open/picker/validate";

    private async Task<int[]> DixJoueursCochesAsync(HttpClient client)
    {
        await OpenSoireeAsync(client);
        var dix = await CreatePlayersAsync(client, "J1", "J2", "J3", "J4", "J5", "J6", "J7", "J8", "J9", "J10");
        await ReadJsonAsync(await client.PutAsJsonAsync(Selection, new { playerIds = dix }, Ct));
        return dix;
    }

    private async Task<JsonElement> PickerDeLaSoireeOuverteAsync(HttpClient client)
    {
        var open = await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct));
        return open.GetProperty("soiree").GetProperty("picker");
    }

    private async Task SetCostAsync(int playerId, decimal cost)
    {
        var admin = Factory.CreateAdminClient();
        (await admin.PutAsJsonAsync($"/players/{playerId}/cost", new { cost }, Ct)).EnsureSuccessStatusCode();
    }

    private static int[] Ids(JsonElement joueurs) =>
        joueurs.EnumerateArray().Select(j => j.GetProperty("playerId").GetInt32()).ToArray();

    private static string? AuTrait(JsonElement picker) => picker.GetProperty("equipeAuTrait").GetString();

    [Fact]
    public async Task Lancer_une_draft_avec_capitaines_et_premier_pick_designes_a_la_main()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DixJoueursCochesAsync(client);

        var picker = await ReadJsonAsync(await client.PostAsJsonAsync(DraftUrl,
            new { capitaines = "Manuels", capitaineA = dix[3], capitaineB = dix[7], premierPick = "B" }, Ct));

        Assert.Equal("Draft", picker.GetProperty("methode").GetString());
        Assert.Equal(dix[3], picker.GetProperty("capitaineA").GetInt32());
        Assert.Equal(dix[7], picker.GetProperty("capitaineB").GetInt32());
        Assert.Equal("B", picker.GetProperty("premierPick").GetString());
        Assert.Equal("B", AuTrait(picker));
        Assert.Equal([dix[3]], Ids(picker.GetProperty("teamA")));
        Assert.Equal([dix[7]], Ids(picker.GetProperty("teamB")));
        Assert.Empty(picker.GetProperty("picks").EnumerateArray());
        Assert.False(picker.GetProperty("canValidate").GetBoolean());
    }

    [Fact]
    public async Task Une_draft_complete_alterne_les_picks_restitue_le_cost_des_equipes_et_se_valide_en_serie()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DixJoueursCochesAsync(client);
        decimal[] costs = [1, 1.5m, 2, 2.5m, 3, 3.5m, 4, 4.5m, 5, 6];
        for (var i = 0; i < dix.Length; i++) await SetCostAsync(dix[i], costs[i]);
        await ReadJsonAsync(await client.PostAsJsonAsync(DraftUrl,
            new { capitaines = "Manuels", capitaineA = dix[0], capitaineB = dix[1], premierPick = "A" }, Ct));

        // A prend J3, B prend J4, A prend J5… : A = J1 J3 J5 J7 J9, B = J2 J4 J6 J8 J10.
        JsonElement picker = default;
        for (var i = 2; i < 10; i++)
        {
            var equipe = i % 2 == 0 ? "A" : "B";
            picker = await ReadJsonAsync(await client.PostAsJsonAsync(Picks, new { equipe, playerId = dix[i] }, Ct));
            Assert.Equal(i == 9 ? null : (i % 2 == 0 ? "B" : "A"), AuTrait(picker));
        }

        Assert.Equal([dix[0], dix[2], dix[4], dix[6], dix[8]], Ids(picker.GetProperty("teamA")));
        Assert.Equal([dix[1], dix[3], dix[5], dix[7], dix[9]], Ids(picker.GetProperty("teamB")));
        Assert.Equal(15m, picker.GetProperty("teamACost").GetDecimal());
        Assert.Equal(18m, picker.GetProperty("teamBCost").GetDecimal());
        Assert.Equal(dix[2..], Ids(picker.GetProperty("picks")));
        Assert.True(picker.GetProperty("canValidate").GetBoolean());

        var response = await client.PostAsJsonAsync(Validation, new { format = 3 }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var seriesId = (await ReadJsonAsync(response)).GetProperty("seriesId").GetInt32();
        var serie = await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));
        Assert.Equal(new[] { dix[0], dix[2], dix[4], dix[6], dix[8] }.Order(), Ids(serie.GetProperty("teamA")).Order());
        Assert.Equal(new[] { dix[1], dix[3], dix[5], dix[7], dix[9] }.Order(), Ids(serie.GetProperty("teamB")).Order());
        Assert.Equal(JsonValueKind.Null, (await PickerDeLaSoireeOuverteAsync(client)).ValueKind);
    }

    [Fact]
    public async Task Le_cost_de_chaque_equipe_est_restitue_pendant_la_draft()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DixJoueursCochesAsync(client);
        await SetCostAsync(dix[0], 4.5m);
        await SetCostAsync(dix[1], 3);
        await SetCostAsync(dix[2], 2.5m);
        await ReadJsonAsync(await client.PostAsJsonAsync(DraftUrl,
            new { capitaines = "Manuels", capitaineA = dix[0], capitaineB = dix[1], premierPick = "B" }, Ct));

        var picker = await ReadJsonAsync(await client.PostAsJsonAsync(Picks, new { equipe = "B", playerId = dix[2] }, Ct));

        Assert.Equal(4.5m, picker.GetProperty("teamACost").GetDecimal());
        Assert.Equal(5.5m, picker.GetProperty("teamBCost").GetDecimal());
    }

    private async Task<int[]> DraftLanceeAsync(HttpClient client, string premierPick = "A")
    {
        var dix = await DixJoueursCochesAsync(client);
        await ReadJsonAsync(await client.PostAsJsonAsync(DraftUrl,
            new { capitaines = "Manuels", capitaineA = dix[0], capitaineB = dix[1], premierPick }, Ct));
        return dix;
    }

    private static void AssertProblem(HttpStatusCode attendu, HttpResponseMessage response)
    {
        Assert.Equal(attendu, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Un_pick_hors_tour_repond_409_et_ne_change_rien()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DraftLanceeAsync(client, premierPick: "A");

        var response = await client.PostAsJsonAsync(Picks, new { equipe = "B", playerId = dix[2] }, Ct);

        AssertProblem(HttpStatusCode.Conflict, response);
        var picker = await PickerDeLaSoireeOuverteAsync(client);
        Assert.Empty(picker.GetProperty("picks").EnumerateArray());
        Assert.Equal("A", AuTrait(picker));
    }

    [Fact]
    public async Task Prendre_un_joueur_deja_pris_ou_un_capitaine_repond_409()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DraftLanceeAsync(client, premierPick: "A");
        await ReadJsonAsync(await client.PostAsJsonAsync(Picks, new { equipe = "A", playerId = dix[2] }, Ct));

        var dejaPris = await client.PostAsJsonAsync(Picks, new { equipe = "B", playerId = dix[2] }, Ct);
        var capitaine = await client.PostAsJsonAsync(Picks, new { equipe = "B", playerId = dix[0] }, Ct);

        AssertProblem(HttpStatusCode.Conflict, dejaPris);
        AssertProblem(HttpStatusCode.Conflict, capitaine);
        var picker = await PickerDeLaSoireeOuverteAsync(client);
        Assert.Equal([dix[2]], Ids(picker.GetProperty("picks")));
        Assert.Equal([dix[0], dix[2]], Ids(picker.GetProperty("teamA")));
        Assert.Equal("B", AuTrait(picker));
    }

    [Fact]
    public async Task Prendre_un_joueur_hors_des_dix_repond_400()
    {
        var client = Factory.CreateSignedInClient();
        await DraftLanceeAsync(client, premierPick: "A");
        var onzieme = await CreatePlayerAsync(client, "J11");

        var response = await client.PostAsJsonAsync(Picks, new { equipe = "A", playerId = onzieme }, Ct);

        AssertProblem(HttpStatusCode.BadRequest, response);
        var picker = await PickerDeLaSoireeOuverteAsync(client);
        Assert.Empty(picker.GetProperty("picks").EnumerateArray());
        Assert.Equal("A", AuTrait(picker));
    }

    [Fact]
    public async Task Un_pick_pour_une_autre_equipe_que_A_ou_B_repond_400()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DraftLanceeAsync(client, premierPick: "A");

        var response = await client.PostAsJsonAsync(Picks, new { equipe = "C", playerId = dix[2] }, Ct);

        AssertProblem(HttpStatusCode.BadRequest, response);
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).GetProperty("detail").GetString();

    [Fact]
    public async Task Un_pick_sans_draft_en_cours_repond_409()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DixJoueursCochesAsync(client);
        await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/roulette", new { variante = "Aleatoire" }, Ct));

        var response = await client.PostAsJsonAsync(Picks, new { equipe = "A", playerId = dix[2] }, Ct);

        AssertProblem(HttpStatusCode.Conflict, response);
        Assert.Contains("Aucune Draft en cours", await DetailAsync(response));
    }

    [Fact]
    public async Task Un_pick_apres_une_draft_complete_repond_409()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DraftLanceeAsync(client, premierPick: "A");
        for (var i = 2; i < 10; i++)
            await ReadJsonAsync(await client.PostAsJsonAsync(Picks, new { equipe = i % 2 == 0 ? "A" : "B", playerId = dix[i] }, Ct));

        var response = await client.PostAsJsonAsync(Picks, new { equipe = "A", playerId = dix[2] }, Ct);

        AssertProblem(HttpStatusCode.Conflict, response);
        Assert.Contains("La Draft est complète", await DetailAsync(response));
    }

    [Fact]
    public async Task Un_capitaine_designe_hors_des_dix_repond_400()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DixJoueursCochesAsync(client);
        var onzieme = await CreatePlayerAsync(client, "J11");

        var response = await client.PostAsJsonAsync(DraftUrl,
            new { capitaines = "Manuels", capitaineA = dix[0], capitaineB = onzieme, premierPick = "A" }, Ct);

        AssertProblem(HttpStatusCode.BadRequest, response);
        Assert.Equal(JsonValueKind.Null, (await PickerDeLaSoireeOuverteAsync(client)).GetProperty("methode").ValueKind);
    }

    [Fact]
    public async Task Un_meme_joueur_designe_deux_fois_capitaine_repond_400()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DixJoueursCochesAsync(client);

        var response = await client.PostAsJsonAsync(DraftUrl,
            new { capitaines = "Manuels", capitaineA = dix[0], capitaineB = dix[0], premierPick = "A" }, Ct);

        AssertProblem(HttpStatusCode.BadRequest, response);
    }

    [Fact]
    public async Task Un_premier_pick_pour_une_autre_equipe_que_A_ou_B_repond_400()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DixJoueursCochesAsync(client);

        var response = await client.PostAsJsonAsync(DraftUrl,
            new { capitaines = "Manuels", capitaineA = dix[0], capitaineB = dix[1], premierPick = "C" }, Ct);

        AssertProblem(HttpStatusCode.BadRequest, response);
    }

    [Fact]
    public async Task Les_capitaines_par_plus_gros_cost_sont_les_deux_plus_gros_costs_des_dix()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DixJoueursCochesAsync(client);
        await SetCostAsync(dix[6], 5.5m);
        await SetCostAsync(dix[2], 6);

        var picker = await ReadJsonAsync(await client.PostAsJsonAsync(DraftUrl,
            new { capitaines = "PlusGrosCosts", premierPick = "A" }, Ct));

        Assert.Equal(dix[2], picker.GetProperty("capitaineA").GetInt32());
        Assert.Equal(dix[6], picker.GetProperty("capitaineB").GetInt32());
    }

    [Fact]
    public async Task Les_capitaines_au_hasard_et_le_pile_ou_face_designent_deux_des_dix_et_une_equipe()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DixJoueursCochesAsync(client);

        var picker = await ReadJsonAsync(await client.PostAsJsonAsync(DraftUrl, new { capitaines = "AuHasard" }, Ct));

        var capitaineA = picker.GetProperty("capitaineA").GetInt32();
        var capitaineB = picker.GetProperty("capitaineB").GetInt32();
        Assert.Contains(capitaineA, dix);
        Assert.Contains(capitaineB, dix);
        Assert.NotEqual(capitaineA, capitaineB);
        Assert.Contains(picker.GetProperty("premierPick").GetString(), new[] { "A", "B" });
        Assert.Equal(picker.GetProperty("premierPick").GetString(), AuTrait(picker));
    }

    [Fact]
    public async Task Une_draft_a_moitie_faite_est_restauree_telle_quelle_a_la_lecture_de_la_soiree()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DraftLanceeAsync(client, premierPick: "B");
        await ReadJsonAsync(await client.PostAsJsonAsync(Picks, new { equipe = "B", playerId = dix[5] }, Ct));
        await ReadJsonAsync(await client.PostAsJsonAsync(Picks, new { equipe = "A", playerId = dix[2] }, Ct));
        var enCours = await ReadJsonAsync(await client.PostAsJsonAsync(Picks, new { equipe = "B", playerId = dix[9] }, Ct));

        var restauree = await PickerDeLaSoireeOuverteAsync(Factory.CreateSignedInClient("1002"));

        Assert.Equal("Draft", restauree.GetProperty("methode").GetString());
        Assert.Equal(dix[0], restauree.GetProperty("capitaineA").GetInt32());
        Assert.Equal(dix[1], restauree.GetProperty("capitaineB").GetInt32());
        Assert.Equal("B", restauree.GetProperty("premierPick").GetString());
        Assert.Equal([dix[5], dix[2], dix[9]], Ids(restauree.GetProperty("picks")));
        Assert.Equal(["B", "A", "B"], restauree.GetProperty("picks").EnumerateArray().Select(p => p.GetProperty("equipe").GetString()));
        Assert.Equal("A", AuTrait(restauree));
        Assert.Equal(Ids(enCours.GetProperty("teamA")), Ids(restauree.GetProperty("teamA")));
        Assert.Equal(Ids(enCours.GetProperty("teamB")), Ids(restauree.GetProperty("teamB")));

        // Et elle reprend où elle en était.
        var suite = await ReadJsonAsync(await client.PostAsJsonAsync(Picks, new { equipe = "A", playerId = dix[3] }, Ct));
        Assert.Equal("B", AuTrait(suite));
    }

    [Fact]
    public async Task Relancer_la_draft_efface_les_picks_et_tirer_la_roulette_efface_la_draft()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DraftLanceeAsync(client, premierPick: "A");
        await ReadJsonAsync(await client.PostAsJsonAsync(Picks, new { equipe = "A", playerId = dix[2] }, Ct));

        var relancee = await ReadJsonAsync(await client.PostAsJsonAsync(DraftUrl,
            new { capitaines = "Manuels", capitaineA = dix[4], capitaineB = dix[5], premierPick = "B" }, Ct));

        Assert.Empty(relancee.GetProperty("picks").EnumerateArray());
        Assert.Equal([dix[4]], Ids(relancee.GetProperty("teamA")));
        Assert.Equal([dix[5]], Ids(relancee.GetProperty("teamB")));

        var roulette = await ReadJsonAsync(await client.PostAsJsonAsync("/soirees/open/picker/roulette", new { variante = "Aleatoire" }, Ct));

        Assert.Equal("Roulette", roulette.GetProperty("methode").GetString());
        Assert.Equal(JsonValueKind.Null, roulette.GetProperty("capitaineA").ValueKind);
        Assert.Equal(JsonValueKind.Null, roulette.GetProperty("premierPick").ValueKind);
        Assert.Equal(JsonValueKind.Null, roulette.GetProperty("equipeAuTrait").ValueKind);
        Assert.Empty(roulette.GetProperty("picks").EnumerateArray());
    }

    [Fact]
    public async Task Les_commandes_de_la_draft_sont_publiees_dans_openapi()
    {
        await using var development = Factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));

        var document = await ReadJsonAsync(await development.CreateClient().GetAsync("/openapi/v1.json", Ct));

        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty(DraftUrl, out _));
        Assert.True(paths.TryGetProperty(Picks, out _));
    }
}
