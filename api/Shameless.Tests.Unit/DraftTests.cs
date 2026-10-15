using Shameless.Application.Interfaces;
using Shameless.Application.Picker;

namespace Shameless.Tests.Unit;

public class DraftTests
{
    private sealed class SeededRandom(int seed) : IRandomSource
    {
        private readonly Random _random = new(seed);
        public int Next(int maxExclusive) => _random.Next(maxExclusive);
    }

    private static List<PickerPlayer> Joueurs(params decimal[] costs) =>
        costs.Select((cost, i) => new PickerPlayer(i + 1, cost)).ToList();

    [Fact]
    public void Les_capitaines_par_plus_gros_cost_sont_les_deux_plus_gros_costs_parmi_les_dix()
    {
        var joueurs = Joueurs(3, 7.5m, 2, 4, 9, 1, 5, 6, 3.5m, 2.5m);

        var capitaines = Draft.CapitainesParPlusGrosCost(joueurs);

        Assert.Equal(5, capitaines.CapitaineA);
        Assert.Equal(2, capitaines.CapitaineB);
    }

    [Fact]
    public void A_cost_egal_les_capitaines_par_plus_gros_cost_suivent_l_ordre_de_la_selection()
    {
        var joueurs = Joueurs(1, 4, 2, 4, 3, 4, 1, 2, 3, 1);

        var capitaines = Draft.CapitainesParPlusGrosCost(joueurs);

        Assert.Equal(2, capitaines.CapitaineA);
        Assert.Equal(4, capitaines.CapitaineB);
    }

    [Fact]
    public void Les_capitaines_au_hasard_sont_deux_joueurs_distincts_parmi_les_dix()
    {
        int[] dix = [11, 12, 13, 14, 15, 16, 17, 18, 19, 20];
        var random = new SeededRandom(3);
        var tires = new HashSet<int>();

        for (var i = 0; i < 50; i++)
        {
            var capitaines = Draft.CapitainesAuHasard(dix, random);
            Assert.NotEqual(capitaines.CapitaineA, capitaines.CapitaineB);
            Assert.Contains(capitaines.CapitaineA, dix);
            Assert.Contains(capitaines.CapitaineB, dix);
            tires.Add(capitaines.CapitaineA);
            tires.Add(capitaines.CapitaineB);
        }

        Assert.True(tires.Count > 2);
    }

    [Fact]
    public void Le_pile_ou_face_donne_le_premier_pick_a_l_une_ou_l_autre_equipe()
    {
        var random = new SeededRandom(5);

        var tirages = Enumerable.Range(0, 20).Select(_ => Draft.PileOuFace(random)).ToHashSet();

        Assert.Equal(["A", "B"], tirages.Order());
    }

    [Theory]
    [InlineData("A", "ABABABAB")]
    [InlineData("B", "BABABABA")]
    public void Les_huit_picks_alternent_un_par_un_a_partir_du_premier_pick(string premierPick, string ordre)
    {
        var equipes = Draft.OrdreDesPicks(premierPick);

        Assert.Equal(ordre, string.Concat(equipes));
    }

    [Theory]
    [InlineData("A", 0, "A")]
    [InlineData("A", 1, "B")]
    [InlineData("B", 0, "B")]
    [InlineData("B", 5, "A")]
    [InlineData("A", 7, "B")]
    public void L_equipe_au_trait_se_deduit_du_premier_pick_et_des_picks_faits(string premierPick, int picksFaits, string auTrait)
    {
        Assert.Equal(auTrait, Draft.EquipeAuTrait(premierPick, picksFaits));
    }

    [Fact]
    public void Apres_huit_picks_plus_aucune_equipe_n_est_au_trait()
    {
        Assert.Null(Draft.EquipeAuTrait("A", 8));
    }
}
