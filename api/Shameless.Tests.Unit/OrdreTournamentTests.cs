using Shameless.Application.ChampionDraft;
using Shameless.Application.Entities;

namespace Shameless.Tests.Unit;

public class OrdreTournamentTests
{
    [Fact]
    public void L_ordre_tournament_suit_les_vingt_tours_fixes_bans_puis_picks()
    {
        // bans B1 R1 B2 R2 B3 R3, picks B1 R1 R2 B2 B3 R3, bans R4 B4 R5 B5, picks R4 B4 B5 R5
        string[] attendu =
        [
            "ban blue", "ban red", "ban blue", "ban red", "ban blue", "ban red",
            "pick blue", "pick red", "pick red", "pick blue", "pick blue", "pick red",
            "ban red", "ban blue", "ban red", "ban blue",
            "pick red", "pick blue", "pick blue", "pick red"
        ];

        var tours = OrdreTournament.Tours;

        Assert.Equal(attendu, tours.Select(t => $"{t.Action} {t.Side}"));
        Assert.Equal(Enumerable.Range(1, 20), tours.Select(t => t.Numero));
    }

    [Fact]
    public void Chaque_side_bannit_cinq_champions_et_en_pick_cinq()
    {
        foreach (var side in new[] { Sides.Blue, Sides.Red })
        {
            Assert.Equal(5, OrdreTournament.Tours.Count(t => t.Side == side && t.Action == ActionsDraft.Ban));
            Assert.Equal(5, OrdreTournament.Tours.Count(t => t.Side == side && t.Action == ActionsDraft.Pick));
        }
    }

    [Theory]
    [InlineData(1, "A", "A")]
    [InlineData(1, "B", "B")]
    [InlineData(2, "A", "B")]
    [InlineData(9, "A", "B")]
    [InlineData(10, "A", "A")]
    [InlineData(13, "B", "A")]
    [InlineData(20, "B", "A")]
    public void L_equipe_attendue_a_un_tour_depend_de_l_equipe_cote_blue(int numero, string equipeBlue, string attendue)
    {
        Assert.Equal(attendue, OrdreTournament.EquipeAttendue(numero, equipeBlue));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(21)]
    public void Un_numero_de_tour_hors_des_vingt_tours_est_refuse(int numero)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => OrdreTournament.Tour(numero));
    }

    [Fact]
    public void Le_tour_courant_suit_les_tours_joues_et_n_existe_plus_quand_la_draft_est_terminee()
    {
        Assert.Equal(1, OrdreTournament.TourCourant(0)?.Numero);
        Assert.Equal(7, OrdreTournament.TourCourant(6)?.Numero);
        Assert.Null(OrdreTournament.TourCourant(20));
    }
}
