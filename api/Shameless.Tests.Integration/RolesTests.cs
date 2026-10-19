using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shameless.Application.Entities;
using Shameless.Application.Services;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>Les trois rôles en base (ADR 0017) : connexion, droits, promotion entre Administrateurs, import de la configuration.</summary>
public class RolesTests(ApiFactory factory) : IntegrationTest(factory)
{
    private const string Admin = TestSession.AdminDiscordId;

    private async Task<User> UserAsync(string discordId)
    {
        await using var db = Factory.CreateDbContext();
        return await db.Users.SingleAsync(u => u.DiscordId == long.Parse(discordId), Ct);
    }

    private async Task<HttpResponseMessage> PromouvoirAsync(HttpClient client, string discordId) =>
        await client.PutAsync($"/utilisateurs/{discordId}/administrateur", null, Ct);

    private async Task<HttpResponseMessage> RetirerAsync(HttpClient client, string discordId) =>
        await client.DeleteAsync($"/utilisateurs/{discordId}/administrateur", Ct);

    [Fact]
    public async Task Un_snowflake_de_19_chiffres_est_promu_et_retire_sans_perte_de_precision()
    {
        const string snowflake = "1234567890123456789";
        Factory.CreateSignedInClient(snowflake, "Gros identifiant");
        var admin = Factory.CreateAdminClient();

        Assert.Equal(HttpStatusCode.OK, (await PromouvoirAsync(admin, snowflake)).StatusCode);
        Assert.Equal(UserRole.Administrateur, (await UserAsync(snowflake)).Role);
        Assert.Equal(HttpStatusCode.OK, (await RetirerAsync(admin, snowflake)).StatusCode);
        Assert.Equal(UserRole.Utilisateur, (await UserAsync(snowflake)).Role);
    }

    [Fact]
    public async Task Un_identifiant_qui_n_est_pas_un_snowflake_est_refuse_en_400()
    {
        var admin = Factory.CreateAdminClient();

        Assert.Equal(HttpStatusCode.BadRequest, (await PromouvoirAsync(admin, "abc")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await RetirerAsync(admin, "-5")).StatusCode);
    }

    [Fact]
    public async Task La_premiere_connexion_cree_la_ligne_utilisateur_et_les_suivantes_relisent_pseudo_et_photo()
    {
        Factory.CreateSignedInClient("6001", "Ancien");
        var cree = await UserAsync("6001");
        Assert.Equal("Ancien", cree.DiscordUsername);
        Assert.Equal(UserRole.Utilisateur, cree.Role);

        Factory.CreateSignedInClient("6001", "Nouveau");

        await using var db = Factory.CreateDbContext();
        var ligne = await db.Users.SingleAsync(u => u.DiscordId == 6001, Ct);
        Assert.Equal(cree.Id, ligne.Id);
        Assert.Equal("Nouveau", ligne.DiscordUsername);
    }

    [Fact]
    public async Task Me_expose_le_role_en_base()
    {
        var utilisateur = await Factory.CreateSignedInClient("6002").GetFromJsonAsync<JsonElement>("/auth/me", Ct);
        var superadmin = await Factory.CreateAdminClient().GetFromJsonAsync<JsonElement>("/auth/me", Ct);

        Assert.Equal("Utilisateur", utilisateur.GetProperty("role").GetString());
        Assert.False(utilisateur.GetProperty("isAdmin").GetBoolean());
        Assert.Equal("Superadministrateur", superadmin.GetProperty("role").GetString());
        Assert.True(superadmin.GetProperty("isAdmin").GetBoolean());
    }

    [Fact]
    public async Task Le_role_en_base_decide_des_droits_sans_nouvelle_connexion()
    {
        var admin = Factory.CreateAdminClient();
        var jeanne = Factory.CreateSignedInClient("6003", "Jeanne");
        Assert.Equal(HttpStatusCode.Forbidden, (await jeanne.PostAsync("/players/6003/peak", null, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await PromouvoirAsync(admin, "6003")).StatusCode);

        var me = await jeanne.GetFromJsonAsync<JsonElement>("/auth/me", Ct);
        Assert.True(me.GetProperty("isAdmin").GetBoolean());
        Assert.Equal("Administrateur", me.GetProperty("role").GetString());
        Assert.NotEqual(HttpStatusCode.Forbidden, (await jeanne.PostAsync("/players/6003/peak", null, Ct)).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await RetirerAsync(admin, "6003")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await jeanne.PostAsync("/players/6003/peak", null, Ct)).StatusCode);
    }

    [Fact]
    public async Task La_promotion_garde_qui_a_promu_et_quand()
    {
        Factory.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero));
        Factory.CreateSignedInClient("6004", "Cible");

        var reponse = await PromouvoirAsync(Factory.CreateAdminClient(), "6004");

        var corps = await reponse.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("admin", corps.GetProperty("promotedBy").GetString());
        Assert.Equal(new DateTime(2026, 10, 18, 12, 0, 0), corps.GetProperty("promotedAt").GetDateTime());
        var ligne = await UserAsync("6004");
        Assert.Equal("admin", ligne.PromotedBy);
        Assert.Equal(new DateTime(2026, 10, 18, 12, 0, 0), ligne.PromotedAt);

        var liste = await Factory.CreateAdminClient().GetFromJsonAsync<JsonElement>("/utilisateurs", Ct);
        Assert.Contains(liste.EnumerateArray(), u => u.GetProperty("discordUserId").GetString() == "6004"
                                                     && u.GetProperty("promotedBy").GetString() == "admin");
    }

    [Fact]
    public async Task Un_utilisateur_simple_ne_promeut_ni_ne_liste()
    {
        Factory.CreateSignedInClient("6005");
        var simple = Factory.CreateSignedInClient("6006");

        Assert.Equal(HttpStatusCode.Forbidden, (await PromouvoirAsync(simple, "6005")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RetirerAsync(simple, "6005")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await simple.GetAsync("/utilisateurs", Ct)).StatusCode);
    }

    [Fact]
    public async Task Promouvoir_quelqu_un_qui_ne_s_est_jamais_connecte_donne_404()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await PromouvoirAsync(Factory.CreateAdminClient(), "6999")).StatusCode);
    }

    [Fact]
    public async Task Un_administrateur_ne_touche_pas_un_superadministrateur_et_ne_se_retire_pas_lui_meme()
    {
        var superadmin = Factory.CreateAdminClient();
        Factory.CreateSignedInClient("6007", "Adm");
        await PromouvoirAsync(superadmin, "6007");
        var administrateur = Factory.CreateSignedInClient("6007", "Adm");

        Assert.Equal(HttpStatusCode.Forbidden, (await RetirerAsync(administrateur, Admin)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await PromouvoirAsync(administrateur, Admin)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await RetirerAsync(administrateur, "6007")).StatusCode);

        Assert.Equal(UserRole.Superadministrateur, (await UserAsync(Admin)).Role);
        Assert.Equal(UserRole.Administrateur, (await UserAsync("6007")).Role);
    }

    [Fact]
    public async Task Un_administrateur_promeut_et_retire_d_autres_administrateurs()
    {
        var superadmin = Factory.CreateAdminClient();
        Factory.CreateSignedInClient("6008");
        Factory.CreateSignedInClient("6009");
        await PromouvoirAsync(superadmin, "6008");
        var premier = Factory.CreateSignedInClient("6008");

        Assert.Equal(HttpStatusCode.OK, (await PromouvoirAsync(premier, "6009")).StatusCode);
        Assert.Equal("Administrateur", (await UserAsync("6009")).Role.ToString());
        Assert.Equal("joueur", (await UserAsync("6009")).PromotedBy);

        Assert.Equal(HttpStatusCode.OK, (await RetirerAsync(premier, "6009")).StatusCode);
        var retire = await UserAsync("6009");
        Assert.Equal(UserRole.Utilisateur, retire.Role);
        Assert.Null(retire.PromotedBy);
    }

    [Fact]
    public async Task Promouvoir_deux_fois_ne_change_pas_la_mention_de_promotion()
    {
        var superadmin = Factory.CreateAdminClient();
        Factory.CreateSignedInClient("6010");
        Factory.CreateSignedInClient("6011", "Second");
        await PromouvoirAsync(superadmin, "6010");
        await PromouvoirAsync(superadmin, "6011");
        var second = Factory.CreateSignedInClient("6011", "Second");

        await PromouvoirAsync(second, "6010");

        Assert.Equal("admin", (await UserAsync("6010")).PromotedBy);
    }

    [Fact]
    public async Task L_import_de_la_configuration_cree_les_superadministrateurs_une_seule_fois()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RolesService>();
        await using (var db = Factory.CreateDbContext())
            await db.Database.ExecuteSqlRawAsync("TRUNCATE users RESTART IDENTITY CASCADE", Ct);

        Assert.Equal(2, await roles.ImportConfiguredSuperadminsAsync([111L, 222L, 111L]));
        // Un redémarrage relit la même configuration : rien ne change, même après un retrait en base.
        Assert.Equal(0, await roles.ImportConfiguredSuperadminsAsync([111L, 222L]));
        Assert.Equal(RoleResult.Ok, await roles.RemoveSuperadminAsync(222));
        Assert.Equal(0, await roles.ImportConfiguredSuperadminsAsync([111L, 222L]));

        var superadmins = await roles.ListSuperadminsAsync();
        var seul = Assert.Single(superadmins);
        Assert.Equal(111, seul.DiscordId);
        Assert.Equal(RolesService.Configuration, seul.PromotedBy);
        Assert.Equal(2, (await roles.ListAsync()).Count);
    }

    [Fact]
    public async Task L_import_promeut_un_utilisateur_deja_connecte_sans_creer_de_doublon()
    {
        Factory.CreateSignedInClient("6020", "Deja");
        await using var scope = Factory.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RolesService>();
        await using (var db = Factory.CreateDbContext())
            await db.Database.ExecuteSqlRawAsync("UPDATE users SET role = 'Utilisateur', promoted_by = NULL, promoted_at = NULL", Ct);

        await roles.ImportConfiguredSuperadminsAsync([6020L]);

        var ligne = await UserAsync("6020");
        Assert.Equal(UserRole.Superadministrateur, ligne.Role);
        Assert.Equal("Deja", ligne.DiscordUsername);
        await using var verif = Factory.CreateDbContext();
        Assert.Equal(1, await verif.Users.CountAsync(u => u.DiscordId == 6020, Ct));
    }

    [Fact]
    public async Task Le_dernier_superadministrateur_ne_se_retire_pas()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RolesService>();

        Assert.Equal(RoleResult.LastSuperadmin, await roles.RemoveSuperadminAsync(long.Parse(Admin)));
        Assert.Equal(RoleResult.NotSuperadmin, await roles.RemoveSuperadminAsync(424242));

        await roles.AddSuperadminAsync(7777);
        Assert.Equal(RoleResult.Ok, await roles.RemoveSuperadminAsync(long.Parse(Admin)));
        Assert.Equal(RoleResult.LastSuperadmin, await roles.RemoveSuperadminAsync(7777));
        Assert.Equal(UserRole.Utilisateur, (await UserAsync(Admin)).Role);
    }

    [Fact]
    public async Task Ajouter_un_superadministrateur_est_idempotent_garde_la_mention_et_cree_la_ligne_si_besoin()
    {
        Factory.Clock.SetUtcNow(new DateTimeOffset(2026, 10, 1, 8, 0, 0, TimeSpan.Zero));
        await using var scope = Factory.Services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RolesService>();

        await roles.AddSuperadminAsync(8888);
        Factory.Clock.SetUtcNow(new DateTimeOffset(2026, 11, 1, 8, 0, 0, TimeSpan.Zero));
        await roles.AddSuperadminAsync(8888);

        var ligne = await UserAsync("8888");
        Assert.Equal(UserRole.Superadministrateur, ligne.Role);
        Assert.Equal(RolesService.LigneDeCommande, ligne.PromotedBy);
        Assert.Equal(new DateTime(2026, 10, 1, 8, 0, 0), ligne.PromotedAt);
        Assert.Equal(2, (await roles.ListSuperadminsAsync()).Count);
    }

    [Fact]
    public async Task Un_superadministrateur_ajoute_par_la_commande_a_les_droits_a_sa_premiere_connexion()
    {
        await using (var scope = Factory.Services.CreateAsyncScope())
            await scope.ServiceProvider.GetRequiredService<RolesService>().AddSuperadminAsync(8889);

        var me = await Factory.CreateSignedInClient("8889", "Nouvelle").GetFromJsonAsync<JsonElement>("/auth/me", Ct);

        Assert.Equal("Superadministrateur", me.GetProperty("role").GetString());
        Assert.Equal("Nouvelle", (await UserAsync("8889")).DiscordUsername);
    }
}
