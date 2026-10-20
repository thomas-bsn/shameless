using Shameless.Application.Entities;
using Shameless.Application.Stats;

namespace Shameless.Tests.Unit;

/// <summary>Les corrections de stats d'un Administrateur appliquées aux données brutes du Companion.</summary>
public class ValeursEffectivesTests
{
    private static CustomGameParticipant Participant(params (ChampCorrigeable Champ, string Valeur)[] corrections) => new()
    {
        PlayerId = 1, Side = "blue",
        RiotParticipantData = """{ "totalMinionsKilled": 150, "neutralMinionsKilled": 30 }""",
        Corrections = corrections.Select(c => new CorrectionDeStat { Champ = c.Champ, Valeur = c.Valeur }).ToList()
    };

    [Fact]
    public void Un_cs_total_seul_remplace_les_sbires_et_met_les_monstres_a_zero()
    {
        var s = ValeursEffectives.Stats(Participant((ChampCorrigeable.Cs, "200")))!;

        Assert.Equal((200, 0), (s.MinionsKilled, s.NeutralMinionsKilled));
    }

    [Fact]
    public void Un_cs_total_et_les_sbires_corriges_derivent_les_monstres_du_reste()
    {
        var s = ValeursEffectives.Stats(Participant((ChampCorrigeable.Cs, "200"), (ChampCorrigeable.MinionsKilled, "170")))!;

        Assert.Equal((170, 30), (s.MinionsKilled, s.NeutralMinionsKilled));
        Assert.Equal(200, FormulesDeStats.Cs(s));
    }

    [Fact]
    public void Un_cs_total_et_les_monstres_corriges_derivent_les_sbires_du_reste_avec_plancher_a_zero()
    {
        var s = ValeursEffectives.Stats(Participant((ChampCorrigeable.NeutralMinionsKilled, "50"), (ChampCorrigeable.Cs, "200")))!;
        Assert.Equal((150, 50), (s.MinionsKilled, s.NeutralMinionsKilled));

        var plancher = ValeursEffectives.Stats(Participant((ChampCorrigeable.Cs, "20"), (ChampCorrigeable.NeutralMinionsKilled, "50")))!;
        Assert.Equal((0, 50), (plancher.MinionsKilled, plancher.NeutralMinionsKilled));
    }
}
