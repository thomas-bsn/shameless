using Shameless.Application.Interfaces;
using Shameless.Application.Picker;

namespace Shameless.Tests.Unit;

public class AutoroleTests
{
    private sealed class SeededRandom(int seed) : IRandomSource
    {
        private readonly Random _random = new(seed);
        public int Next(int maxExclusive) => _random.Next(maxExclusive);
    }

    private static readonly Composition Equipes = new([1, 2, 3, 4, 5], [6, 7, 8, 9, 10]);

    private static readonly Role[] CinqRoles = [Role.Top, Role.Jungle, Role.Mid, Role.Adc, Role.Support];

    private static Dictionary<int, Role?> SansRolePrincipal() =>
        Enumerable.Range(1, 10).ToDictionary(id => id, _ => (Role?)null);

    private static void AssertUnRoleDeChaqueParEquipe(IReadOnlyDictionary<int, Role> roles)
    {
        Assert.Equal(CinqRoles.Order(), Equipes.TeamA.Select(id => roles[id]).Order());
        Assert.Equal(CinqRoles.Order(), Equipes.TeamB.Select(id => roles[id]).Order());
    }

    [Fact]
    public void L_autorole_aleatoire_donne_a_chaque_equipe_un_top_un_jungle_un_mid_un_adc_et_un_support()
    {
        var roles = Autorole.Attribuer(AutoroleVariante.Aleatoire, Equipes, SansRolePrincipal(), new SeededRandom(1));

        AssertUnRoleDeChaqueParEquipe(roles);
    }

    private static int SurLeurRolePrincipal(IReadOnlyDictionary<int, Role> roles, IReadOnlyDictionary<int, Role?> principaux) =>
        roles.Count(r => principaux[r.Key] == r.Value);

    [Fact]
    public void L_autorole_hors_role_ne_met_personne_sur_son_role_principal_quand_c_est_possible()
    {
        // Chaque Équipe a un main de chaque rôle.
        var principaux = SansRolePrincipal();
        for (var i = 0; i < 5; i++)
        {
            principaux[Equipes.TeamA[i]] = CinqRoles[i];
            principaux[Equipes.TeamB[i]] = CinqRoles[i];
        }

        for (var graine = 0; graine < 20; graine++)
        {
            var roles = Autorole.Attribuer(AutoroleVariante.HorsRole, Equipes, principaux, new SeededRandom(graine));

            AssertUnRoleDeChaqueParEquipe(roles);
            Assert.Equal(0, SurLeurRolePrincipal(roles, principaux));
        }
    }

    [Fact]
    public void Quand_le_hors_role_est_impossible_l_autorole_minimise_les_joueurs_sur_leur_role_principal()
    {
        // Équipe A : cinq mains mid. Le mid revient forcément à l'un d'eux, les quatre autres jouent
        // hors rôle. Équipe B : un main de chaque rôle, personne n'y joue son rôle principal.
        var principaux = SansRolePrincipal();
        foreach (var id in Equipes.TeamA)
            principaux[id] = Role.Mid;
        for (var i = 0; i < 5; i++)
            principaux[Equipes.TeamB[i]] = CinqRoles[i];

        for (var graine = 0; graine < 20; graine++)
        {
            var roles = Autorole.Attribuer(AutoroleVariante.HorsRole, Equipes, principaux, new SeededRandom(graine));

            AssertUnRoleDeChaqueParEquipe(roles);
            Assert.Equal(1, SurLeurRolePrincipal(roles, principaux));
            Assert.All(Equipes.TeamB, id => Assert.NotEqual(principaux[id], roles[id]));
        }
    }

    [Theory]
    [InlineData(AutoroleVariante.HorsRole)]
    [InlineData(AutoroleVariante.Aleatoire)]
    public void Un_joueur_sans_role_principal_peut_recevoir_n_importe_quel_role(AutoroleVariante variante)
    {
        // Le joueur 1 n'a pas de rôle principal ; ses coéquipiers sont mains jungle, mid, ADC et support.
        var principaux = SansRolePrincipal();
        principaux[2] = Role.Jungle;
        principaux[3] = Role.Mid;
        principaux[4] = Role.Adc;
        principaux[5] = Role.Support;
        var random = new SeededRandom(3);

        var rolesDuJoueur1 = Enumerable.Range(0, 200)
            .Select(_ => Autorole.Attribuer(variante, Equipes, principaux, random)[1])
            .ToHashSet();

        Assert.Equal(CinqRoles.Order(), rolesDuJoueur1.Order());
    }

    [Theory]
    [InlineData(AutoroleVariante.HorsRole)]
    [InlineData(AutoroleVariante.Aleatoire)]
    public void Une_relance_peut_donner_une_autre_attribution(AutoroleVariante variante)
    {
        var random = new SeededRandom(5);

        var attributions = Enumerable.Range(0, 20)
            .Select(_ => string.Join(",", Autorole.Attribuer(variante, Equipes, SansRolePrincipal(), random).OrderBy(r => r.Key).Select(r => r.Value)))
            .ToHashSet();

        Assert.True(attributions.Count > 1);
    }

    [Fact]
    public void L_autorole_ne_change_pas_la_composition_des_equipes()
    {
        var roles = Autorole.Attribuer(AutoroleVariante.HorsRole, Equipes, SansRolePrincipal(), new SeededRandom(1));

        Assert.Equal(Enumerable.Range(1, 10), roles.Keys.Order());
    }

    [Theory]
    [InlineData("TOP", Role.Top)]
    [InlineData("JUNGLE", Role.Jungle)]
    [InlineData("MIDDLE", Role.Mid)]
    [InlineData("BOTTOM", Role.Adc)]
    [InlineData("UTILITY", Role.Support)]
    [InlineData(null, null)]
    [InlineData("", null)]
    public void Le_role_principal_se_lit_depuis_la_position_riot(string? mainRole, Role? attendu)
    {
        Assert.Equal(attendu, Autorole.RolePrincipal(mainRole));
    }
}
