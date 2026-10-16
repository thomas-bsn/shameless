using Shameless.Application.ChampionDraft;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Application.Picker;

namespace Shameless.Tests.Unit;

public class DesignationDesCapitainesTests
{
    /// <summary>Un aléa qui rend toujours le même tirage.</summary>
    private sealed class TirageFixe(int tirage) : IRandomSource
    {
        public int Next(int maxExclusive) => tirage;
    }

    private static readonly IRandomSource SansAlea = new TirageFixe(0);

    /// <summary>Une Équipe de cinq dans l'ordre de sa composition : ids 11 à 15.</summary>
    private static List<PickerPlayer> Equipe(params decimal[] costs) =>
        costs.Select((cost, i) => new PickerPlayer(11 + i, cost)).ToList();

    [Fact]
    public void Manuel_designe_le_joueur_choisi_de_l_equipe()
    {
        var capitaine = DesignationDesCapitaines.Designer(DesignationCapitaine.Manuel, Equipe(1, 2, 3, 4, 5), 13, SansAlea);

        Assert.Equal(13, capitaine);
    }

    [Fact]
    public void Manuel_ne_designe_personne_hors_de_l_equipe()
    {
        Assert.Null(DesignationDesCapitaines.Designer(DesignationCapitaine.Manuel, Equipe(1, 2, 3, 4, 5), 99, SansAlea));
        Assert.Null(DesignationDesCapitaines.Designer(DesignationCapitaine.Manuel, Equipe(1, 2, 3, 4, 5), null, SansAlea));
    }

    [Fact]
    public void Le_plus_gros_cost_de_l_equipe_est_designe()
    {
        var capitaine = DesignationDesCapitaines.Designer(DesignationCapitaine.PlusGrosCost, Equipe(3, 7.5m, 2, 9, 4), null, SansAlea);

        Assert.Equal(14, capitaine);
    }

    [Fact]
    public void A_cost_egal_l_ordre_de_la_composition_departage()
    {
        var capitaine = DesignationDesCapitaines.Designer(DesignationCapitaine.PlusGrosCost, Equipe(3, 8, 2, 8, 4), null, SansAlea);

        Assert.Equal(12, capitaine);
    }

    [Theory]
    [InlineData(0, 11)]
    [InlineData(2, 13)]
    [InlineData(4, 15)]
    public void Au_hasard_le_tirage_designe_un_joueur_de_l_equipe(int tirage, int attendu)
    {
        var capitaine = DesignationDesCapitaines.Designer(DesignationCapitaine.AuHasard, Equipe(1, 2, 3, 4, 5), null, new TirageFixe(tirage));

        Assert.Equal(attendu, capitaine);
    }
}
