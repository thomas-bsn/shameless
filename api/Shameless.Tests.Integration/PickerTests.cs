using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class PickerTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const string Selection = "/soirees/open/picker/selection";
    private const string RouletteUrl = "/soirees/open/picker/roulette";
    private const string Validation = "/soirees/open/picker/validate";

    private async Task<int[]> DixJoueursAsync(HttpClient client) =>
        await CreatePlayersAsync(client, "J1", "J2", "J3", "J4", "J5", "J6", "J7", "J8", "J9", "J10");

    private async Task<JsonElement> PickerDeLaSoireeOuverteAsync(HttpClient client)
    {
        var open = await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct));
        return open.GetProperty("soiree").GetProperty("picker");
    }

    private static int[] Ids(JsonElement joueurs) =>
        joueurs.EnumerateArray().Select(j => j.GetProperty("playerId").GetInt32()).ToArray();

    private static string Cle(JsonElement picker) =>
        string.Join(",", Ids(picker.GetProperty("teamA")).Order()) + "|" + string.Join(",", Ids(picker.GetProperty("teamB")).Order());

    private static void AssertDeuxEquipesDeCinq(JsonElement picker, int[] dix)
    {
        var teamA = Ids(picker.GetProperty("teamA"));
        var teamB = Ids(picker.GetProperty("teamB"));
        Assert.Equal(5, teamA.Length);
        Assert.Equal(5, teamB.Length);
        Assert.Equal(dix.Order(), teamA.Concat(teamB).Order());
    }

    private async Task SetCostAsync(int playerId, decimal cost)
    {
        var admin = Factory.CreateAdminClient();
        (await admin.PutAsJsonAsync($"/players/{playerId}/cost", new { cost }, Ct)).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Sans_session_de_picker_la_soiree_ouverte_n_a_pas_de_picker()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);

        var picker = await PickerDeLaSoireeOuverteAsync(client);

        Assert.Equal(JsonValueKind.Null, picker.ValueKind);
    }

    [Fact]
    public async Task Cocher_dix_joueurs_ouvre_une_session_de_picker_restituee_avec_la_soiree_ouverte()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var dix = await DixJoueursAsync(client);

        var response = await client.PutAsJsonAsync(Selection, new { playerIds = dix }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var picker = await PickerDeLaSoireeOuverteAsync(Factory.CreateSignedInClient("1002"));
        Assert.Equal(dix, Ids(picker.GetProperty("players")));
        Assert.Equal(JsonValueKind.Null, picker.GetProperty("methode").ValueKind);
        Assert.Empty(picker.GetProperty("teamA").EnumerateArray());
        Assert.Empty(picker.GetProperty("teamB").EnumerateArray());
        Assert.False(picker.GetProperty("canValidate").GetBoolean());
    }

    [Theory]
    [InlineData(9)]
    [InlineData(11)]
    public async Task Une_selection_qui_n_a_pas_exactement_dix_joueurs_repond_400(int nombre)
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var joueurs = await CreatePlayersAsync(client, Enumerable.Range(1, 11).Select(i => $"J{i}").ToArray());

        var response = await client.PutAsJsonAsync(Selection, new { playerIds = joueurs[..nombre] }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal(JsonValueKind.Null, (await PickerDeLaSoireeOuverteAsync(client)).ValueKind);
    }

    [Fact]
    public async Task Une_selection_avec_un_joueur_en_double_ou_inconnu_repond_400()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var dix = await DixJoueursAsync(client);

        var doublon = await client.PutAsJsonAsync(Selection, new { playerIds = dix[..9].Append(dix[0]) }, Ct);
        var inconnu = await client.PutAsJsonAsync(Selection, new { playerIds = dix[..9].Append(9999) }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, doublon.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, inconnu.StatusCode);
    }

    [Fact]
    public async Task Sans_soiree_ouverte_le_picker_repond_409()
    {
        var client = Factory.CreateSignedInClient();
        var dix = await DixJoueursAsync(client);

        var response = await client.PutAsJsonAsync(Selection, new { playerIds = dix }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Tirer_sans_session_de_picker_repond_409()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);

        var response = await client.PostAsJsonAsync(RouletteUrl, new { variante = "Aleatoire" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task La_roulette_aleatoire_forme_deux_equipes_de_cinq_parmi_les_dix()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var dix = await DixJoueursAsync(client);
        await ReadJsonAsync(await client.PutAsJsonAsync(Selection, new { playerIds = dix }, Ct));

        var picker = await ReadJsonAsync(await client.PostAsJsonAsync(RouletteUrl, new { variante = "Aleatoire" }, Ct));

        Assert.Equal("Roulette", picker.GetProperty("methode").GetString());
        Assert.Equal("Aleatoire", picker.GetProperty("variante").GetString());
        AssertDeuxEquipesDeCinq(picker, dix);
        Assert.True(picker.GetProperty("canValidate").GetBoolean());
    }

    [Fact]
    public async Task La_roulette_equilibree_donne_un_ecart_de_cost_d_au_plus_1_et_le_restitue()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var dix = await DixJoueursAsync(client);
        decimal[] costs = [1, 1.5m, 2, 2.5m, 3, 3.5m, 4, 4.5m, 5, 6];
        for (var i = 0; i < dix.Length; i++) await SetCostAsync(dix[i], costs[i]);
        await ReadJsonAsync(await client.PutAsJsonAsync(Selection, new { playerIds = dix }, Ct));

        var picker = await ReadJsonAsync(await client.PostAsJsonAsync(RouletteUrl, new { variante = "Equilibree" }, Ct));

        AssertDeuxEquipesDeCinq(picker, dix);
        var costOf = dix.Zip(costs).ToDictionary(p => p.First, p => p.Second);
        var teamACost = Ids(picker.GetProperty("teamA")).Sum(id => costOf[id]);
        var teamBCost = Ids(picker.GetProperty("teamB")).Sum(id => costOf[id]);
        Assert.Equal(teamACost, picker.GetProperty("teamACost").GetDecimal());
        Assert.Equal(teamBCost, picker.GetProperty("teamBCost").GetDecimal());
        Assert.Equal(Math.Abs(teamACost - teamBCost), picker.GetProperty("ecartCost").GetDecimal());
        Assert.True(picker.GetProperty("ecartCost").GetDecimal() <= 1m);
    }

    [Fact]
    public async Task Une_roulette_equilibree_relancee_peut_donner_d_autres_equipes()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var dix = await DixJoueursAsync(client);
        await ReadJsonAsync(await client.PutAsJsonAsync(Selection, new { playerIds = dix }, Ct));

        var tirages = new HashSet<string>();
        for (var i = 0; i < 5; i++)
            tirages.Add(Cle(await ReadJsonAsync(await client.PostAsJsonAsync(RouletteUrl, new { variante = "Equilibree" }, Ct))));

        Assert.True(tirages.Count > 1);
    }

    [Fact]
    public async Task Un_tirage_non_valide_est_restaure_a_la_lecture_de_la_soiree()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var dix = await DixJoueursAsync(client);
        await ReadJsonAsync(await client.PutAsJsonAsync(Selection, new { playerIds = dix }, Ct));
        var tirage = await ReadJsonAsync(await client.PostAsJsonAsync(RouletteUrl, new { variante = "Equilibree" }, Ct));

        var restaure = await PickerDeLaSoireeOuverteAsync(Factory.CreateSignedInClient("1002"));

        Assert.Equal(Ids(tirage.GetProperty("teamA")), Ids(restaure.GetProperty("teamA")));
        Assert.Equal(Ids(tirage.GetProperty("teamB")), Ids(restaure.GetProperty("teamB")));
        Assert.Equal("Roulette", restaure.GetProperty("methode").GetString());
        Assert.Equal("Equilibree", restaure.GetProperty("variante").GetString());
        Assert.Equal(tirage.GetProperty("ecartCost").GetDecimal(), restaure.GetProperty("ecartCost").GetDecimal());
    }

    [Fact]
    public async Task Changer_la_selection_remplace_les_dix_joueurs_et_efface_le_tirage()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var onze = await CreatePlayersAsync(client, Enumerable.Range(1, 11).Select(i => $"J{i}").ToArray());
        await ReadJsonAsync(await client.PutAsJsonAsync(Selection, new { playerIds = onze[..10] }, Ct));
        await ReadJsonAsync(await client.PostAsJsonAsync(RouletteUrl, new { variante = "Aleatoire" }, Ct));

        var autre = onze[1..];
        await ReadJsonAsync(await client.PutAsJsonAsync(Selection, new { playerIds = autre }, Ct));

        var picker = await PickerDeLaSoireeOuverteAsync(client);
        Assert.Equal(autre, Ids(picker.GetProperty("players")));
        Assert.Equal(JsonValueKind.Null, picker.GetProperty("methode").ValueKind);
        Assert.Empty(picker.GetProperty("teamA").EnumerateArray());
    }

    [Fact]
    public async Task Valider_le_tirage_cree_la_serie_avec_ses_equipes_et_clot_la_session()
    {
        var client = Factory.CreateSignedInClient();
        var soireeId = await OpenSoireeAsync(client);
        var dix = await DixJoueursAsync(client);
        await ReadJsonAsync(await client.PutAsJsonAsync(Selection, new { playerIds = dix }, Ct));
        var tirage = await ReadJsonAsync(await client.PostAsJsonAsync(RouletteUrl, new { variante = "Aleatoire" }, Ct));

        var response = await client.PostAsJsonAsync(Validation, new { format = 5, fearless = true, alternateSides = true }, Ct);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var seriesId = (await ReadJsonAsync(response)).GetProperty("seriesId").GetInt32();
        var serie = await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}", Ct));
        Assert.Equal(soireeId, serie.GetProperty("soireeId").GetInt32());
        Assert.Equal(5, serie.GetProperty("format").GetInt32());
        Assert.True(serie.GetProperty("fearless").GetBoolean());
        Assert.True(serie.GetProperty("alternateSides").GetBoolean());
        Assert.Equal("EnCours", serie.GetProperty("outcome").GetString());
        Assert.Equal(Ids(tirage.GetProperty("teamA")).Order(), Ids(serie.GetProperty("teamA")).Order());
        Assert.Equal(Ids(tirage.GetProperty("teamB")).Order(), Ids(serie.GetProperty("teamB")).Order());
        Assert.Equal(JsonValueKind.Null, (await PickerDeLaSoireeOuverteAsync(client)).ValueKind);
    }

    [Fact]
    public async Task Valider_sans_tirage_repond_409_et_garde_la_session()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var dix = await DixJoueursAsync(client);
        await ReadJsonAsync(await client.PutAsJsonAsync(Selection, new { playerIds = dix }, Ct));

        var response = await client.PostAsJsonAsync(Validation, new { format = 3 }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.NotEqual(JsonValueKind.Null, (await PickerDeLaSoireeOuverteAsync(client)).ValueKind);
    }

    [Fact]
    public async Task Valider_avec_un_format_invalide_repond_400_et_garde_le_tirage()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var dix = await DixJoueursAsync(client);
        await ReadJsonAsync(await client.PutAsJsonAsync(Selection, new { playerIds = dix }, Ct));
        var tirage = await ReadJsonAsync(await client.PostAsJsonAsync(RouletteUrl, new { variante = "Aleatoire" }, Ct));

        var response = await client.PostAsJsonAsync(Validation, new { format = 4 }, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var picker = await PickerDeLaSoireeOuverteAsync(client);
        Assert.Equal(Ids(tirage.GetProperty("teamA")), Ids(picker.GetProperty("teamA")));
    }

    [Fact]
    public async Task Les_commandes_du_picker_sont_publiees_dans_openapi()
    {
        await using var development = Factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));

        var document = await ReadJsonAsync(await development.CreateClient().GetAsync("/openapi/v1.json", Ct));

        var paths = document.GetProperty("paths");
        Assert.True(paths.TryGetProperty(Selection, out _));
        Assert.True(paths.TryGetProperty(RouletteUrl, out _));
        Assert.True(paths.TryGetProperty(Validation, out _));
    }
}
