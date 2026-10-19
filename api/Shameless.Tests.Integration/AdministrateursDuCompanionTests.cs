using System.Net;
using Shameless.Tests.Integration.Infrastructure;

namespace Shameless.Tests.Integration;

/// <summary>
/// La route publique du Companion (ADR 0020) : à qui donner le Fichier de Rapport quand l'envoi
/// échoue. Sans connexion, les pseudos Discord du Superadministrateur et des Administrateurs, rien d'autre.
/// </summary>
public class AdministrateursDuCompanionTests(ApiFactory factory) : IntegrationTest(factory)
{
    [Fact]
    public async Task La_route_renvoie_les_pseudos_du_superadministrateur_et_des_administrateurs_sans_connexion()
    {
        var superadmin = Factory.CreateAdminClient();
        Factory.CreateSignedInClient("6101", "Marie");
        Factory.CreateSignedInClient("6102", "Simple utilisateur");
        Assert.Equal(HttpStatusCode.OK, (await superadmin.PutAsync("/utilisateurs/6101/administrateur", null, Ct)).StatusCode);

        var reponse = await Factory.CreateAnonymousClient().GetAsync("/companion/administrateurs", Ct);

        Assert.Equal(HttpStatusCode.OK, reponse.StatusCode);
        var corps = await ReadJsonAsync(reponse);
        Assert.Equal(["superadministrateurs", "administrateurs"], corps.EnumerateObject().Select(p => p.Name));
        Assert.Equal(["admin"], corps.GetProperty("superadministrateurs").EnumerateArray().Select(p => p.GetString()));
        Assert.Equal(["Marie"], corps.GetProperty("administrateurs").EnumerateArray().Select(p => p.GetString()));
    }
}
