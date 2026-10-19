using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Shameless.Application.Riot;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

public class ActualisationRiotTests(ApiFactory factory) : IntegrationTest(factory)
{
    private async Task<JsonElement> JoueurAsync(HttpClient client, int playerId)
    {
        var players = await client.GetFromJsonAsync<JsonElement>("/players", Ct);
        return players.EnumerateArray().Single(p => p.GetProperty("id").GetInt32() == playerId);
    }

    private async Task<JsonElement> EtatAsync(HttpClient client) =>
        await ReadJsonAsync(await client.GetAsync("/admin/riot", Ct));

    private async Task<JsonElement> AttendreLaFinAsync(HttpClient client)
    {
        for (var i = 0; i < 200; i++)
        {
            var etat = await EtatAsync(client);
            if (!etat.GetProperty("enCours").GetBoolean()) return etat;
            await Task.Delay(25, Ct);
        }
        throw new TimeoutException("L'actualisation ne se termine pas");
    }

    [Fact]
    public async Task Un_admin_actualise_les_rangs_et_le_role_principal_d_un_joueur()
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        var id = await CreatePlayerAsync(admin, "Ancien", "GOLD", "II");
        Factory.Riot.AddAccount("Ancien#EUW", "MASTER", null, lp: 200, mainRole: "JUNGLE", flexTier: "SILVER", flexDivision: "I");

        var reponse = await ReadJsonAsync(await admin.PostAsync($"/admin/riot/joueurs/{id}", null, Ct));

        Assert.True(reponse.GetProperty("rangsActualises").GetBoolean());
        Assert.True(reponse.GetProperty("roleActualise").GetBoolean());
        Assert.Empty(reponse.GetProperty("echecs").EnumerateArray());
        var joueur = await JoueurAsync(admin, id);
        Assert.Equal("MASTER", joueur.GetProperty("rankTier").GetString());
        Assert.Equal(200, joueur.GetProperty("lp").GetInt32());
        Assert.Equal("JUNGLE", joueur.GetProperty("mainRole").GetString());
    }

    [Fact]
    public async Task Un_echec_riot_sur_le_role_garde_l_ancien_role_principal_et_est_signale()
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        var id = await CreatePlayerAsync(admin, "Garde");
        Factory.Riot.AddAccount("Garde#EUW", "GOLD", "II", mainRole: "TOP");
        await admin.PostAsync($"/admin/riot/joueurs/{id}", null, Ct);
        Factory.Riot.AddAccount("Garde#EUW", "PLATINUM", "IV", mainRole: "BOTTOM");
        Factory.Riot.RolesIndisponibles.Add("Garde#EUW");

        var reponse = await ReadJsonAsync(await admin.PostAsync($"/admin/riot/joueurs/{id}", null, Ct));

        Assert.True(reponse.GetProperty("rangsActualises").GetBoolean());
        Assert.False(reponse.GetProperty("roleActualise").GetBoolean());
        Assert.Single(reponse.GetProperty("echecs").EnumerateArray());
        var joueur = await JoueurAsync(admin, id);
        Assert.Equal("PLATINUM", joueur.GetProperty("rankTier").GetString());
        Assert.Equal("TOP", joueur.GetProperty("mainRole").GetString());
    }

    [Fact]
    public async Task Un_echec_riot_sur_les_rangs_garde_les_anciens_rangs_et_est_signale()
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        var id = await CreatePlayerAsync(admin, "Rangs", "GOLD", "II");
        Factory.Riot.AddAccount("Rangs#EUW", "MASTER", null, mainRole: "MIDDLE");
        Factory.Riot.RangsIndisponibles = true;

        var reponse = await ReadJsonAsync(await admin.PostAsync($"/admin/riot/joueurs/{id}", null, Ct));

        Assert.False(reponse.GetProperty("rangsActualises").GetBoolean());
        Assert.Single(reponse.GetProperty("echecs").EnumerateArray());
        Assert.Equal("GOLD", (await JoueurAsync(admin, id)).GetProperty("rankTier").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden, "clé Riot refusée (403)")]
    [InlineData(HttpStatusCode.TooManyRequests, "limite de requêtes Riot atteinte (429)")]
    [InlineData(HttpStatusCode.InternalServerError, "panne côté Riot (500)")]
    [InlineData(HttpStatusCode.NotFound, "joueur introuvable chez Riot (404)")]
    public async Task L_echec_sur_le_role_expose_la_cause_et_garde_l_ancien_role(HttpStatusCode statut, string cause)
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        var id = await CreatePlayerAsync(admin, "Cause");
        Factory.Riot.AddAccount("Cause#EUW", "GOLD", "II", mainRole: "TOP");
        await admin.PostAsync($"/admin/riot/joueurs/{id}", null, Ct);
        Factory.Riot.RolesIndisponibles.Add("Cause#EUW");
        Factory.Riot.StatutRoles = statut;

        var reponse = await ReadJsonAsync(await admin.PostAsync($"/admin/riot/joueurs/{id}", null, Ct));

        Assert.True(reponse.GetProperty("rangsActualises").GetBoolean());
        Assert.False(reponse.GetProperty("roleActualise").GetBoolean());
        Assert.Equal(JsonValueKind.Null, reponse.GetProperty("causeRangs").ValueKind);
        Assert.Contains(cause, reponse.GetProperty("causeRole").GetString());
        var message = Assert.Single(reponse.GetProperty("echecs").EnumerateArray()).GetString();
        Assert.Contains(cause, message);
        Assert.Contains("l'ancien rôle est conservé", message);
        Assert.Equal("TOP", (await JoueurAsync(admin, id)).GetProperty("mainRole").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "limite de requêtes Riot atteinte (429)")]
    [InlineData(HttpStatusCode.InternalServerError, "panne côté Riot (500)")]
    public async Task L_echec_sur_les_rangs_expose_la_cause_et_garde_les_anciens_rangs(HttpStatusCode statut, string cause)
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        var id = await CreatePlayerAsync(admin, "Cause", "GOLD", "II");
        Factory.Riot.AddAccount("Cause#EUW", "MASTER", null, mainRole: "MIDDLE");
        Factory.Riot.RangsIndisponibles = true;
        Factory.Riot.StatutRangs = statut;

        var reponse = await ReadJsonAsync(await admin.PostAsync($"/admin/riot/joueurs/{id}", null, Ct));

        Assert.False(reponse.GetProperty("rangsActualises").GetBoolean());
        Assert.True(reponse.GetProperty("roleActualise").GetBoolean());
        Assert.Contains(cause, reponse.GetProperty("causeRangs").GetString());
        Assert.Equal(JsonValueKind.Null, reponse.GetProperty("causeRole").ValueKind);
        var message = Assert.Single(reponse.GetProperty("echecs").EnumerateArray()).GetString();
        Assert.Contains(cause, message);
        Assert.Contains("Riot n'a pas répondu, les anciens rangs sont conservés", message);
        Assert.Equal("GOLD", (await JoueurAsync(admin, id)).GetProperty("rankTier").GetString());
    }

    [Fact]
    public async Task Tout_actualiser_liste_la_cause_de_chaque_echec_par_joueur()
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        await CreatePlayerAsync(admin, "Refuse");
        Factory.Riot.AddAccount("Refuse#EUW", "GOLD", "II");
        Factory.Riot.RangsIndisponibles = true;
        Factory.Riot.StatutRangs = HttpStatusCode.Forbidden;

        await admin.PostAsync("/admin/riot/tous", null, Ct);
        var fin = await AttendreLaFinAsync(admin);

        var echec = Assert.Single(fin.GetProperty("echecs").EnumerateArray());
        Assert.Equal("Refuse", echec.GetProperty("prenom").GetString());
        Assert.Contains("clé Riot refusée (403)", echec.GetProperty("message").GetString());
    }

    private int AppelsRole() => Factory.Riot.Requetes.Count(c => c.Contains("/lol/match/v5/"));

    private int AppelsRangs() => Factory.Riot.Requetes.Count(c => c.Contains("/lol/league/v4/"));

    [Fact]
    public async Task Une_cle_refusee_sur_les_rangs_n_appelle_pas_le_role_et_expose_le_code()
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        var id = await CreatePlayerAsync(admin, "Refuse", "GOLD", "II");
        Factory.Riot.AddAccount("Refuse#EUW", "MASTER", null, mainRole: "MIDDLE");
        Factory.Riot.RangsIndisponibles = true;
        Factory.Riot.StatutRangs = HttpStatusCode.Forbidden;
        Factory.Riot.Requetes.Clear();

        var reponse = await ReadJsonAsync(await admin.PostAsync($"/admin/riot/joueurs/{id}", null, Ct));

        Assert.Equal(1, AppelsRangs());
        Assert.Equal(0, AppelsRole());
        Assert.False(reponse.GetProperty("rangsActualises").GetBoolean());
        Assert.False(reponse.GetProperty("roleActualise").GetBoolean());
        Assert.Equal("CleRefusee", reponse.GetProperty("codeCauseRangs").GetString());
        Assert.Equal("CleRefusee", reponse.GetProperty("codeCauseRole").GetString());
        Assert.True(reponse.GetProperty("riotBloque").GetBoolean());
        Assert.Contains("clé Riot refusée (403)", reponse.GetProperty("causeRangs").GetString());
    }

    [Fact]
    public async Task Tout_actualiser_s_arrete_a_la_premiere_cle_refusee_et_marque_les_autres_non_tentes()
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        foreach (var nom in new[] { "Un", "Deux", "Trois" })
        {
            await CreatePlayerAsync(admin, nom);
            Factory.Riot.AddAccount($"{nom}#EUW", "GOLD", "II");
        }
        Factory.Riot.RangsIndisponibles = true;
        Factory.Riot.StatutRangs = HttpStatusCode.Forbidden;

        Factory.Riot.Requetes.Clear();
        await admin.PostAsync("/admin/riot/tous", null, Ct);
        var fin = await AttendreLaFinAsync(admin);

        Assert.Equal(1, AppelsRangs());
        Assert.Equal(0, AppelsRole());
        Assert.Equal(3, fin.GetProperty("termines").GetInt32());
        var echecs = fin.GetProperty("echecs").EnumerateArray().Select(e => e.GetProperty("message").GetString()!).ToList();
        Assert.Equal(3, echecs.Count);
        Assert.Contains("clé Riot refusée (403)", echecs[0]);
        Assert.All(echecs.Skip(1), m => Assert.Contains("Non tentée", m));
    }

    [Fact]
    public async Task Tout_actualiser_s_arrete_sur_une_limite_dont_l_attente_depasse_la_politique()
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        foreach (var nom in new[] { "Un", "Deux" })
        {
            await CreatePlayerAsync(admin, nom);
            Factory.Riot.AddAccount($"{nom}#EUW", "GOLD", "II");
        }
        Factory.Riot.RangsIndisponibles = true;
        Factory.Riot.StatutRangs = HttpStatusCode.TooManyRequests;
        Factory.Riot.RetryAfterRangs = TimeSpan.FromSeconds(120);

        Factory.Riot.Requetes.Clear();
        await admin.PostAsync("/admin/riot/tous", null, Ct);
        var fin = await AttendreLaFinAsync(admin);

        Assert.Equal(1, AppelsRangs());
        var echecs = fin.GetProperty("echecs").EnumerateArray().Select(e => e.GetProperty("message").GetString()!).ToList();
        Assert.Contains("limite de requêtes Riot atteinte", echecs[0]);
        Assert.Contains("Non tentée", Assert.Single(echecs.Skip(1)));
    }

    [Fact]
    public async Task Une_panne_de_riot_n_arrete_pas_tout_actualiser()
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        foreach (var nom in new[] { "Un", "Deux" })
        {
            await CreatePlayerAsync(admin, nom);
            Factory.Riot.AddAccount($"{nom}#EUW", "GOLD", "II");
        }
        Factory.Riot.RangsIndisponibles = true;

        Factory.Riot.Requetes.Clear();
        await admin.PostAsync("/admin/riot/tous", null, Ct);
        await AttendreLaFinAsync(admin);

        Assert.Equal(2, AppelsRangs());
    }

    [Fact]
    public async Task Un_joueur_sans_match_n_a_plus_de_role_principal()
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        var id = await CreatePlayerAsync(admin, "Calme");
        Factory.Riot.AddAccount("Calme#EUW", "GOLD", "II", mainRole: "SUPPORT");
        await admin.PostAsync($"/admin/riot/joueurs/{id}", null, Ct);
        Factory.Riot.AddAccount("Calme#EUW", "GOLD", "II", mainRole: null);

        await admin.PostAsync($"/admin/riot/joueurs/{id}", null, Ct);

        Assert.Equal(JsonValueKind.Null, (await JoueurAsync(admin, id)).GetProperty("mainRole").ValueKind);
    }

    [Fact]
    public async Task Sans_cle_riot_l_etat_l_indique_et_les_actualisations_sont_refusees()
    {
        var admin = Factory.CreateAdminClient();
        var id = await CreatePlayerAsync(admin, "Sans");

        var etat = await EtatAsync(admin);
        var un = await admin.PostAsync($"/admin/riot/joueurs/{id}", null, Ct);
        var tous = await admin.PostAsync("/admin/riot/tous", null, Ct);

        Assert.False(etat.GetProperty("disponible").GetBoolean());
        await AssertRefusAsync(un, HttpStatusCode.ServiceUnavailable);
        await AssertRefusAsync(tous, HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task Actualiser_un_joueur_inconnu_renvoie_404()
    {
        Factory.CleRiotConfiguree = true;

        var reponse = await Factory.CreateAdminClient().PostAsync("/admin/riot/joueurs/999", null, Ct);

        await AssertRefusAsync(reponse, HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Tout_actualiser_tourne_en_tache_de_fond_avec_une_progression_et_les_echecs()
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        var a = await CreatePlayerAsync(admin, "Un", "GOLD", "II");
        var b = await CreatePlayerAsync(admin, "Deux", "GOLD", "II");
        Factory.Riot.AddAccount("Un#EUW", "DIAMOND", "I", mainRole: "TOP");
        Factory.Riot.AddAccount("Deux#EUW", "DIAMOND", "II", mainRole: "TOP");
        Factory.Riot.RolesIndisponibles.Add("Deux#EUW");
        Factory.Riot.Barriere = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

        var lancement = await admin.PostAsync("/admin/riot/tous", null, Ct);

        Assert.Equal(HttpStatusCode.Accepted, lancement.StatusCode);
        var enCours = await EtatAsync(admin);
        Assert.True(enCours.GetProperty("enCours").GetBoolean());
        Assert.Equal(2, enCours.GetProperty("total").GetInt32());
        Assert.Equal(0, enCours.GetProperty("termines").GetInt32());

        Factory.Riot.Barriere.SetResult();
        var fin = await AttendreLaFinAsync(admin);

        Assert.Equal(2, fin.GetProperty("termines").GetInt32());
        var echec = Assert.Single(fin.GetProperty("echecs").EnumerateArray());
        Assert.Equal(b, echec.GetProperty("playerId").GetInt32());
        Assert.Equal("DIAMOND", (await JoueurAsync(admin, a)).GetProperty("rankTier").GetString());
        Assert.Equal("TOP", (await JoueurAsync(admin, a)).GetProperty("mainRole").GetString());
    }

    [Fact]
    public async Task Un_second_lancement_pendant_qu_un_autre_tourne_est_refuse_avec_un_message_clair()
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        var id = await CreatePlayerAsync(admin, "Occupe");
        Factory.Riot.Barriere = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        await admin.PostAsync("/admin/riot/tous", null, Ct);

        var tous = await admin.PostAsync("/admin/riot/tous", null, Ct);
        var un = await admin.PostAsync($"/admin/riot/joueurs/{id}", null, Ct);

        await AssertRefusAsync(tous, HttpStatusCode.Conflict);
        await AssertRefusAsync(un, HttpStatusCode.Conflict);
        Factory.Riot.Barriere.SetResult();
        await AttendreLaFinAsync(admin);
        Assert.True((await admin.PostAsync($"/admin/riot/joueurs/{id}", null, Ct)).IsSuccessStatusCode);
    }

    [Fact]
    public async Task La_tache_de_24_h_partage_le_verrou_avec_les_lancements_manuels()
    {
        Factory.CleRiotConfiguree = true;
        var admin = Factory.CreateAdminClient();
        await CreatePlayerAsync(admin, "Partage");
        Factory.Riot.Barriere = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var coordinateur = Factory.Services.GetRequiredService<ActualisationRiot>();

        await admin.PostAsync("/admin/riot/tous", null, Ct);
        var tacheDe24h = await coordinateur.ExecuterSiLibreAsync("Tâche de 24 h", () => Task.CompletedTask);
        Factory.Riot.Barriere.SetResult();
        await AttendreLaFinAsync(admin);
        var apres = await coordinateur.ExecuterSiLibreAsync("Tâche de 24 h", () => Task.CompletedTask);

        Assert.False(tacheDe24h);
        Assert.True(apres);
    }

    [Fact]
    public async Task Un_utilisateur_non_admin_ne_peut_pas_actualiser()
    {
        Factory.CleRiotConfiguree = true;
        var client = Factory.CreateSignedInClient("7101");

        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin/riot", Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/admin/riot/tous", null, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync("/admin/riot/joueurs/1", null, Ct)).StatusCode);
    }
}
