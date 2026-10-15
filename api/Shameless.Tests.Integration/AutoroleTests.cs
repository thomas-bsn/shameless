using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class AutoroleTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const string Selection = "/soirees/open/picker/selection";
    private const string RouletteUrl = "/soirees/open/picker/roulette";
    private const string AutoroleUrl = "/soirees/open/picker/autorole";
    private const string Validation = "/soirees/open/picker/validate";

    private static readonly string[] CinqRoles = ["Adc", "Jungle", "Mid", "Support", "Top"];

    /// <summary>Position Riot du rôle principal → rôle attribué par l'Autorole.</summary>
    private static readonly Dictionary<string, string> RoleDeLaPosition = new()
    {
        ["TOP"] = "Top", ["JUNGLE"] = "Jungle", ["MIDDLE"] = "Mid", ["BOTTOM"] = "Adc", ["UTILITY"] = "Support"
    };

    private async Task<int[]> DixJoueursAsync(HttpClient client) =>
        await CreatePlayersAsync(client, "J1", "J2", "J3", "J4", "J5", "J6", "J7", "J8", "J9", "J10");

    /// <summary>Enregistre un joueur dont le rôle principal lu chez Riot est <paramref name="mainRole"/>.</summary>
    private async Task<int> CreatePlayerWithMainRoleAsync(HttpClient client, string prenom, string? mainRole)
    {
        var riotId = $"{prenom}#EUW";
        Factory.Riot.AddAccount(riotId, "GOLD", "II", mainRole: mainRole);
        return (await ReadJsonAsync(await client.PostAsJsonAsync("/players", new { prenom, riotId }, Ct))).GetProperty("id").GetInt32();
    }

    /// <summary>Dix joueurs, deux mains de chaque rôle : aucune Équipe n'a cinq fois le même rôle principal.</summary>
    private async Task<Dictionary<int, string>> DixJoueursDeDeuxMainsParRoleAsync(HttpClient client)
    {
        var principaux = new Dictionary<int, string>();
        var positions = RoleDeLaPosition.Keys.ToArray();
        for (var i = 0; i < 10; i++)
        {
            var position = positions[i % 5];
            principaux[await CreatePlayerWithMainRoleAsync(client, $"J{i + 1}", position)] = RoleDeLaPosition[position];
        }
        return principaux;
    }

    private async Task<JsonElement> PickerDeLaSoireeOuverteAsync(HttpClient client) =>
        (await ReadJsonAsync(await client.GetAsync("/soirees/open", Ct))).GetProperty("soiree").GetProperty("picker");

    private async Task<JsonElement> EquipesTireesAsync(HttpClient client, IEnumerable<int> dix)
    {
        await ReadJsonAsync(await client.PutAsJsonAsync(Selection, new { playerIds = dix }, Ct));
        return await ReadJsonAsync(await client.PostAsJsonAsync(RouletteUrl, new { variante = "Aleatoire" }, Ct));
    }

    private static int[] Ids(JsonElement joueurs) =>
        joueurs.EnumerateArray().Select(j => j.GetProperty("playerId").GetInt32()).ToArray();

    /// <summary>Le rôle attribué de chaque joueur d'une Équipe.</summary>
    private static Dictionary<int, string?> Roles(JsonElement equipe) =>
        equipe.EnumerateArray().ToDictionary(j => j.GetProperty("playerId").GetInt32(), j => j.GetProperty("role").GetString());

    private static void AssertUnRoleDeChaque(JsonElement equipe) =>
        Assert.Equal(CinqRoles, Roles(equipe).Values.Order());

    [Fact]
    public async Task L_autorole_sans_session_de_picker_repond_409()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);

        var response = await client.PostAsJsonAsync(AutoroleUrl, new { variante = "HorsRole" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task L_autorole_avant_la_formation_des_equipes_repond_409()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var dix = await DixJoueursAsync(client);
        await ReadJsonAsync(await client.PutAsJsonAsync(Selection, new { playerIds = dix }, Ct));

        var response = await client.PostAsJsonAsync(AutoroleUrl, new { variante = "Aleatoire" }, Ct);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task L_autorole_hors_role_attribue_a_chaque_equipe_ses_cinq_roles_sans_rien_changer_aux_equipes()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var principaux = await DixJoueursDeDeuxMainsParRoleAsync(client);
        var tirage = await EquipesTireesAsync(client, principaux.Keys);

        var response = await client.PostAsJsonAsync(AutoroleUrl, new { variante = "HorsRole" }, Ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Restitué à la lecture, pour un autre joueur connecté.
        var picker = await PickerDeLaSoireeOuverteAsync(Factory.CreateSignedInClient("1002"));
        Assert.Equal(Ids(tirage.GetProperty("teamA")), Ids(picker.GetProperty("teamA")));
        Assert.Equal(Ids(tirage.GetProperty("teamB")), Ids(picker.GetProperty("teamB")));
        AssertUnRoleDeChaque(picker.GetProperty("teamA"));
        AssertUnRoleDeChaque(picker.GetProperty("teamB"));
        var roles = Roles(picker.GetProperty("teamA")).Concat(Roles(picker.GetProperty("teamB")));
        Assert.All(roles, r => Assert.NotEqual(principaux[r.Key], r.Value));
    }

    [Fact]
    public async Task Les_roles_attribues_sont_conserves_sur_la_serie_creee()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        await EquipesTireesAsync(client, await DixJoueursAsync(client));
        var autorole = await ReadJsonAsync(await client.PostAsJsonAsync(AutoroleUrl, new { variante = "Aleatoire" }, Ct));

        var created = await ReadJsonAsync(await client.PostAsJsonAsync(Validation, new { format = 3 }, Ct));

        var serie = await ReadJsonAsync(await client.GetAsync($"/series/{created.GetProperty("seriesId").GetInt32()}", Ct));
        Assert.Equal(Roles(autorole.GetProperty("teamA")), Roles(serie.GetProperty("teamA")));
        Assert.Equal(Roles(autorole.GetProperty("teamB")), Roles(serie.GetProperty("teamB")));
        AssertUnRoleDeChaque(serie.GetProperty("teamA"));
        AssertUnRoleDeChaque(serie.GetProperty("teamB"));
    }

    [Theory]
    [InlineData("HorsRole")]
    [InlineData("Aleatoire")]
    public async Task Relancer_l_autorole_peut_donner_une_autre_attribution_sans_changer_les_equipes(string variante)
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        var tirage = await EquipesTireesAsync(client, (await DixJoueursDeDeuxMainsParRoleAsync(client)).Keys);

        var attributions = new HashSet<string>();
        for (var relance = 0; relance < 10; relance++)
        {
            var picker = await ReadJsonAsync(await client.PostAsJsonAsync(AutoroleUrl, new { variante }, Ct));
            Assert.Equal(Ids(tirage.GetProperty("teamA")), Ids(picker.GetProperty("teamA")));
            Assert.Equal(Ids(tirage.GetProperty("teamB")), Ids(picker.GetProperty("teamB")));
            AssertUnRoleDeChaque(picker.GetProperty("teamA"));
            AssertUnRoleDeChaque(picker.GetProperty("teamB"));
            attributions.Add(string.Join(",", Roles(picker.GetProperty("teamA")).Concat(Roles(picker.GetProperty("teamB")))));
        }

        Assert.True(attributions.Count > 1);
    }

    [Fact]
    public async Task Retirer_les_equipes_efface_les_roles_attribues()
    {
        var client = Factory.CreateSignedInClient();
        await OpenSoireeAsync(client);
        await EquipesTireesAsync(client, await DixJoueursAsync(client));
        await ReadJsonAsync(await client.PostAsJsonAsync(AutoroleUrl, new { variante = "Aleatoire" }, Ct));

        var tirage = await ReadJsonAsync(await client.PostAsJsonAsync(RouletteUrl, new { variante = "Aleatoire" }, Ct));

        var roles = Roles(tirage.GetProperty("teamA")).Concat(Roles(tirage.GetProperty("teamB")));
        Assert.All(roles, r => Assert.Null(r.Value));
    }

    [Fact]
    public async Task L_autorole_est_publie_dans_openapi()
    {
        await using var development = Factory.WithWebHostBuilder(b => b.UseEnvironment("Development"));

        var document = await ReadJsonAsync(await development.CreateClient().GetAsync("/openapi/v1.json", Ct));

        Assert.True(document.GetProperty("paths").TryGetProperty(AutoroleUrl, out _));
    }
}
