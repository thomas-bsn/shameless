using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>Les liens d'accès d'une Série (ADR 0010) : un Lien capitaine par Équipe et un Lien spectateur.</summary>
public class LiensAccesTests(ApiFactory factory) : IntegrationTest(factory)
{
    private async Task<JsonElement> LiensAsync(HttpClient client, int seriesId) =>
        await ReadJsonAsync(await client.GetAsync($"/series/{seriesId}/liens", Ct));

    private static string Lien(JsonElement liens, string nom) => liens.GetProperty(nom).GetString()!;

    private async Task<HttpResponseMessage> ResoudreAsync(string lien, HttpClient? client = null) =>
        await (client ?? Factory.CreateAnonymousClient()).GetAsync($"/liens/{Uri.EscapeDataString(lien)}", Ct);

    [Fact]
    public async Task L_organisateur_lit_deux_liens_capitaine_distincts_longs_et_stables_pour_toute_la_serie()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 3);

        var avant = await LiensAsync(client, serie.SeriesId);
        await PlayAndWinAsync(client, serie.SeriesId, "A");
        await PlayGameAsync(client, serie.SeriesId, "red");
        var apres = await LiensAsync(client, serie.SeriesId);

        var a = Lien(avant, "lienCapitaineA");
        var b = Lien(avant, "lienCapitaineB");
        Assert.NotEqual(a, b);
        Assert.True(a.Length >= 40, $"Lien trop court : {a}");
        Assert.True(b.Length >= 40, $"Lien trop court : {b}");
        Assert.Equal(a, Lien(apres, "lienCapitaineA"));
        Assert.Equal(b, Lien(apres, "lienCapitaineB"));
    }

    [Fact]
    public async Task Chaque_serie_a_ses_propres_liens()
    {
        var client = Factory.CreateSignedInClient();
        var premiere = await CreateSerieAsync(client, format: 1);
        await PlayAndWinAsync(client, premiere.SeriesId, "A");
        var revanche = await ReadJsonAsync(await client.PostAsJsonAsync($"/series/{premiere.SeriesId}/rematch", new { format = 1 }, Ct));

        var liens1 = await LiensAsync(client, premiere.SeriesId);
        var liens2 = await LiensAsync(client, revanche.GetProperty("seriesId").GetInt32());

        Assert.NotEqual(Lien(liens1, "lienCapitaineA"), Lien(liens2, "lienCapitaineA"));
        Assert.NotEqual(Lien(liens1, "lienCapitaineB"), Lien(liens2, "lienCapitaineB"));
    }

    [Fact]
    public async Task Un_lien_capitaine_donne_acces_a_sa_serie_et_son_equipe_seulement()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var liens = await LiensAsync(client, serie.SeriesId);

        var a = await ReadJsonAsync(await ResoudreAsync(Lien(liens, "lienCapitaineA")));
        var b = await ReadJsonAsync(await ResoudreAsync(Lien(liens, "lienCapitaineB")));

        Assert.Equal(serie.SeriesId, a.GetProperty("seriesId").GetInt32());
        Assert.Equal("Capitaine", a.GetProperty("portee").GetString());
        Assert.Equal("A", a.GetProperty("equipe").GetString());
        Assert.Equal(serie.SeriesId, b.GetProperty("seriesId").GetInt32());
        Assert.Equal("B", b.GetProperty("equipe").GetString());
    }

    [Fact]
    public async Task Un_lien_ne_cree_ni_session_ni_cookie_et_n_ouvre_aucune_autre_commande()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var lien = Lien(await LiensAsync(client, serie.SeriesId), "lienCapitaineA");
        var capitaine = Factory.CreateAnonymousClient();

        var resolu = await ResoudreAsync(lien, capitaine);

        Assert.Equal(HttpStatusCode.OK, resolu.StatusCode);
        Assert.False(resolu.Headers.Contains("Set-Cookie"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await capitaine.GetAsync($"/series/{serie.SeriesId}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await capitaine.GetAsync($"/series/{serie.SeriesId}/liens?lien={lien}", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await capitaine.GetAsync("/auth/me", Ct)).StatusCode);
    }

    [Fact]
    public async Task La_resolution_d_un_lien_n_expose_aucun_lien()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var liens = await LiensAsync(client, serie.SeriesId);

        foreach (var nom in new[] { "lienCapitaineA", "lienCapitaineB", "lienSpectateur" })
        {
            var corps = await (await ResoudreAsync(Lien(liens, nom))).Content.ReadAsStringAsync(Ct);
            Assert.DoesNotContain(Lien(liens, "lienCapitaineA"), corps);
            Assert.DoesNotContain(Lien(liens, "lienCapitaineB"), corps);
            Assert.DoesNotContain(Lien(liens, "lienSpectateur"), corps);
        }
    }

    [Fact]
    public async Task Un_lien_inconnu_est_refuse()
    {
        var response = await ResoudreAsync("pas-un-vrai-lien-pas-un-vrai-lien-pas-un-vrai");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Renouveler_le_lien_capitaine_d_une_equipe_invalide_l_ancien_seulement()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var avant = await LiensAsync(client, serie.SeriesId);

        var renouveles = await ReadJsonAsync(await client.PostAsync($"/series/{serie.SeriesId}/liens/A/renouveler", null, Ct));

        var nouveau = Lien(renouveles, "lienCapitaineA");
        Assert.NotEqual(Lien(avant, "lienCapitaineA"), nouveau);
        Assert.Equal(Lien(avant, "lienCapitaineB"), Lien(renouveles, "lienCapitaineB"));
        Assert.Equal(nouveau, Lien(await LiensAsync(client, serie.SeriesId), "lienCapitaineA"));
        Assert.Equal(HttpStatusCode.NotFound, (await ResoudreAsync(Lien(avant, "lienCapitaineA"))).StatusCode);
        var resolu = await ReadJsonAsync(await ResoudreAsync(nouveau));
        Assert.Equal("A", resolu.GetProperty("equipe").GetString());
        Assert.Equal(HttpStatusCode.OK, (await ResoudreAsync(Lien(avant, "lienCapitaineB"))).StatusCode);
    }

    [Fact]
    public async Task Renouveler_le_lien_d_une_equipe_inconnue_ou_d_une_serie_inconnue_est_refuse()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);

        var equipe = await client.PostAsync($"/series/{serie.SeriesId}/liens/C/renouveler", null, Ct);
        var inconnue = await client.PostAsync("/series/999/liens/A/renouveler", null, Ct);

        Assert.Equal(HttpStatusCode.BadRequest, equipe.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, inconnue.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/series/999/liens", Ct)).StatusCode);
    }

    [Fact]
    public async Task Le_lien_spectateur_est_disponible_des_la_creation_de_la_serie()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);

        var liens = await LiensAsync(client, serie.SeriesId);

        var spectateur = Lien(liens, "lienSpectateur");
        Assert.True(spectateur.Length >= 40, $"Lien trop court : {spectateur}");
        Assert.False(liens.TryGetProperty("liensCapitaineEnvoyes", out _));
        var resolu = await ReadJsonAsync(await ResoudreAsync(spectateur));
        Assert.Equal(serie.SeriesId, resolu.GetProperty("seriesId").GetInt32());
        Assert.Equal("Spectateur", resolu.GetProperty("portee").GetString());
        Assert.Equal(JsonValueKind.Null, resolu.GetProperty("equipe").ValueKind);
    }

    [Fact]
    public async Task La_route_indiquant_l_envoi_des_liens_capitaine_n_existe_plus()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);

        var response = await client.PostAsync($"/series/{serie.SeriesId}/liens/envoyes", null, Ct);

        Assert.True(response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.MethodNotAllowed, $"{response.StatusCode}");
    }

    [Fact]
    public async Task Les_liens_restent_lisibles_apres_la_fin_de_la_serie()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client, format: 1);
        var avant = await LiensAsync(client, serie.SeriesId);
        await PlayAndWinAsync(client, serie.SeriesId, "B");

        var apres = await LiensAsync(client, serie.SeriesId);

        Assert.Equal(Lien(avant, "lienCapitaineA"), Lien(apres, "lienCapitaineA"));
        Assert.Equal(HttpStatusCode.OK, (await ResoudreAsync(Lien(apres, "lienCapitaineA"))).StatusCode);
    }

    [Fact]
    public async Task Un_visiteur_non_connecte_ne_lit_ni_ne_renouvelle_les_liens()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var avant = await LiensAsync(client, serie.SeriesId);
        var anonyme = Factory.CreateAnonymousClient();

        var lire = await anonyme.GetAsync($"/series/{serie.SeriesId}/liens", Ct);
        var renouveler = await anonyme.PostAsync($"/series/{serie.SeriesId}/liens/A/renouveler", null, Ct);

        Assert.Equal(HttpStatusCode.Unauthorized, lire.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, renouveler.StatusCode);
        var apres = await LiensAsync(client, serie.SeriesId);
        Assert.Equal(Lien(avant, "lienCapitaineA"), Lien(apres, "lienCapitaineA"));
    }

    [Fact]
    public async Task La_lecture_d_une_serie_n_expose_aucun_lien()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var liens = await LiensAsync(client, serie.SeriesId);

        var corps = await (await client.GetAsync($"/series/{serie.SeriesId}", Ct)).Content.ReadAsStringAsync(Ct);

        Assert.DoesNotContain(Lien(liens, "lienCapitaineA"), corps);
        Assert.DoesNotContain(Lien(liens, "lienCapitaineB"), corps);
    }

    private static DateTime? RejointLe(JsonElement liens, string equipe)
    {
        var valeur = liens.GetProperty($"capitaine{equipe}RejointLe");
        return valeur.ValueKind == JsonValueKind.Null ? null : valeur.GetDateTime();
    }

    private async Task<HttpResponseMessage> LancerAsync(HttpClient client, int seriesId) =>
        await client.PostAsync($"/series/{seriesId}/games", null, Ct);

    [Fact]
    public async Task Aucun_capitaine_n_a_rejoint_a_la_creation_de_la_serie()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);

        var liens = await LiensAsync(client, serie.SeriesId);

        Assert.Null(RejointLe(liens, "A"));
        Assert.Null(RejointLe(liens, "B"));
    }

    [Fact]
    public async Task La_premiere_ouverture_d_un_lien_capitaine_est_memorisee_et_un_rechargement_ne_change_rien()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var lien = Lien(await LiensAsync(client, serie.SeriesId), "lienCapitaineA");
        var premiere = new DateTimeOffset(2026, 10, 19, 21, 4, 0, TimeSpan.Zero);
        Factory.Clock.SetUtcNow(premiere);

        await ResoudreAsync(lien);
        Factory.Clock.Advance(TimeSpan.FromMinutes(5));
        await ResoudreAsync(lien);

        var liens = await LiensAsync(client, serie.SeriesId);
        Assert.Equal(premiere.UtcDateTime, RejointLe(liens, "A"));
        Assert.Null(RejointLe(liens, "B"));
    }

    [Fact]
    public async Task Ouvrir_le_lien_spectateur_ne_fait_rejoindre_personne()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);

        await ResoudreAsync(Lien(await LiensAsync(client, serie.SeriesId), "lienSpectateur"));

        var liens = await LiensAsync(client, serie.SeriesId);
        Assert.Null(RejointLe(liens, "A"));
        Assert.Null(RejointLe(liens, "B"));
    }

    [Fact]
    public async Task La_game_ne_se_cree_que_quand_les_deux_capitaines_ont_rejoint()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var liens = await LiensAsync(client, serie.SeriesId);

        Assert.Equal(HttpStatusCode.Conflict, (await LancerAsync(client, serie.SeriesId)).StatusCode);

        await ResoudreAsync(Lien(liens, "lienCapitaineA"));
        Assert.Equal(HttpStatusCode.Conflict, (await LancerAsync(client, serie.SeriesId)).StatusCode);

        await ResoudreAsync(Lien(liens, "lienCapitaineB"));
        Assert.Equal(HttpStatusCode.Created, (await LancerAsync(client, serie.SeriesId)).StatusCode);
    }

    [Fact]
    public async Task Renouveler_un_lien_capitaine_remet_son_equipe_a_pas_rejoint()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        var liens = await LiensAsync(client, serie.SeriesId);
        await ResoudreAsync(Lien(liens, "lienCapitaineA"));
        await ResoudreAsync(Lien(liens, "lienCapitaineB"));

        await client.PostAsync($"/series/{serie.SeriesId}/liens/A/renouveler", null, Ct);

        var apres = await LiensAsync(client, serie.SeriesId);
        Assert.Null(RejointLe(apres, "A"));
        Assert.NotNull(RejointLe(apres, "B"));
        Assert.Equal(HttpStatusCode.Conflict, (await LancerAsync(client, serie.SeriesId)).StatusCode);
    }

    [Fact]
    public async Task Changer_de_capitaine_remet_son_equipe_a_pas_rejoint_mais_pas_le_designer_a_nouveau()
    {
        var client = Factory.CreateSignedInClient();
        var serie = await CreateSerieAsync(client);
        await client.PutAsJsonAsync($"/series/{serie.SeriesId}/capitaines/A", new { designation = "Manuel", playerId = serie.TeamA[0] }, Ct);
        var liens = await LiensAsync(client, serie.SeriesId);
        await ResoudreAsync(Lien(liens, "lienCapitaineA"));
        await ResoudreAsync(Lien(liens, "lienCapitaineB"));

        await client.PutAsJsonAsync($"/series/{serie.SeriesId}/capitaines/A", new { designation = "Manuel", playerId = serie.TeamA[0] }, Ct);
        Assert.NotNull(RejointLe(await LiensAsync(client, serie.SeriesId), "A"));

        await client.PutAsJsonAsync($"/series/{serie.SeriesId}/capitaines/A", new { designation = "Manuel", playerId = serie.TeamA[1] }, Ct);
        var apres = await LiensAsync(client, serie.SeriesId);
        Assert.Null(RejointLe(apres, "A"));
        Assert.NotNull(RejointLe(apres, "B"));
    }
}
