using Shameless.Application.Interfaces;
using Shameless.Application.Picker;

namespace Shameless.Tests.Unit;

public class RouletteTests
{
    private sealed class SeededRandom(int seed) : IRandomSource
    {
        private readonly Random _random = new(seed);
        public int Next(int maxExclusive) => _random.Next(maxExclusive);
    }

    private static readonly int[] Dix = [1, 2, 3, 4, 5, 6, 7, 8, 9, 10];

    private static List<PickerPlayer> Joueurs(params decimal[] costs) =>
        costs.Select((cost, i) => new PickerPlayer(i + 1, cost)).ToList();

    private static void AssertPartitionDesDix(Composition composition, IEnumerable<int> dix)
    {
        Assert.Equal(5, composition.TeamA.Count);
        Assert.Equal(5, composition.TeamB.Count);
        Assert.Equal(dix.Order(), composition.TeamA.Concat(composition.TeamB).Order());
    }

    private static string Cle(Composition c) =>
        string.Join(",", c.TeamA.Order()) + "|" + string.Join(",", c.TeamB.Order());

    [Fact]
    public void La_roulette_aleatoire_forme_deux_equipes_de_cinq_qui_partitionnent_les_dix()
    {
        var composition = Roulette.Aleatoire(Dix, new SeededRandom(1));

        AssertPartitionDesDix(composition, Dix);
    }

    [Fact]
    public void La_roulette_aleatoire_donne_des_equipes_differentes_d_un_tirage_a_l_autre()
    {
        var random = new SeededRandom(7);

        var tirages = Enumerable.Range(0, 20).Select(_ => Cle(Roulette.Aleatoire(Dix, random))).ToHashSet();

        Assert.True(tirages.Count > 1);
    }

    [Fact]
    public void Il_existe_126_partitions_distinctes_des_dix_en_deux_equipes_de_cinq()
    {
        var partitions = Roulette.Partitions(Dix).ToList();

        Assert.Equal(126, partitions.Count);
        Assert.All(partitions, p => AssertPartitionDesDix(p, Dix));
        // {A, B} et {B, A} sont la même partition : aucune n'apparaît deux fois, dans un sens ou dans l'autre.
        var cles = partitions.Select(p => string.Join(",", p.TeamA.Order())).ToHashSet();
        Assert.Equal(126, cles.Count);
        Assert.DoesNotContain(partitions, p => cles.Contains(string.Join(",", p.TeamB.Order())));
    }

    [Fact]
    public void La_roulette_equilibree_respecte_un_ecart_de_cost_d_au_plus_1()
    {
        var joueurs = Joueurs(1, 1.5m, 2, 2.5m, 3, 3.5m, 4, 4.5m, 5, 6);
        var random = new SeededRandom(3);

        for (var i = 0; i < 30; i++)
        {
            var composition = Roulette.Equilibree(joueurs, random);
            AssertPartitionDesDix(composition, Dix);
            Assert.True(Roulette.EcartCost(composition, joueurs) <= 1m);
        }
    }

    [Fact]
    public void Une_roulette_equilibree_relancee_donne_d_autres_equipes()
    {
        var joueurs = Joueurs(1, 1.5m, 2, 2.5m, 3, 3.5m, 4, 4.5m, 5, 6);
        var random = new SeededRandom(5);

        var tirages = Enumerable.Range(0, 10).Select(_ => Cle(Roulette.Equilibree(joueurs, random))).ToHashSet();

        Assert.True(tirages.Count > 1);
    }

    [Fact]
    public void Sans_composition_d_ecart_au_plus_1_la_roulette_equilibree_retombe_sur_l_ecart_minimal()
    {
        // Un joueur à 6, un à 4 et huit à 1 : ensemble 6+4+3 = 13 contre 5 (écart 8) ;
        // séparés 6+4 = 10 contre 4+4 = 8 (écart 2). Aucune partition n'a un écart ≤ 1.
        var joueurs = Joueurs(6, 4, 1, 1, 1, 1, 1, 1, 1, 1);
        var random = new SeededRandom(11);

        for (var i = 0; i < 20; i++)
        {
            var composition = Roulette.Equilibree(joueurs, random);
            AssertPartitionDesDix(composition, Dix);
            Assert.Equal(2m, Roulette.EcartCost(composition, joueurs));
        }
    }

    [Fact]
    public void La_roulette_equilibree_ne_met_pas_toujours_le_premier_joueur_en_equipe_A()
    {
        var joueurs = Joueurs(1, 1, 1, 1, 1, 1, 1, 1, 1, 1);
        var random = new SeededRandom(13);

        var equipesDuPremier = Enumerable.Range(0, 20)
            .Select(_ => Roulette.Equilibree(joueurs, random).TeamA.Contains(1))
            .ToHashSet();

        Assert.Equal(2, equipesDuPremier.Count);
    }

    [Fact]
    public void L_ecart_de_cost_est_la_difference_absolue_des_costs_totaux_des_deux_equipes()
    {
        var joueurs = Joueurs(1, 2, 3, 4, 5, 6, 1, 1, 1, 1);
        var composition = new Composition([1, 2, 3, 4, 5], [6, 7, 8, 9, 10]);

        // A : 1+2+3+4+5 = 15 ; B : 6+1+1+1+1 = 10.
        Assert.Equal(5m, Roulette.EcartCost(composition, joueurs));
    }
}
