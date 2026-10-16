using Shameless.Application.Picker;
using Shameless.Application.Stats;

namespace Shameless.Tests.Unit;

/// <summary>
/// Le Companion transmet les stats de la League Client API locale sans contrat figé : la lecture
/// accepte les formes connues de la LCU et ne fait jamais échouer une game pour une stat illisible.
/// </summary>
public class LectureDesStatsLcuTests
{
    [Fact]
    public void Les_stats_imbriquees_de_l_historique_de_match_lcu_sont_lues_avec_la_lane_de_la_timeline()
    {
        var stats = StatsDuParticipant.Lire("""
            { "championId": 64, "stats": { "kills": 4, "deaths": 3, "assists": 11, "totalMinionsKilled": 40,
              "neutralMinionsKilled": 150, "visionScore": 33 }, "timeline": { "lane": "JUNGLE" } }
            """);

        Assert.NotNull(stats);
        Assert.Equal(4, stats.Kills);
        Assert.Equal(11, stats.Assists);
        Assert.Equal(150, stats.NeutralMinionsKilled);
        Assert.Equal(33, stats.VisionScore);
        Assert.Equal(Role.Jungle, stats.Position);
    }

    [Fact]
    public void Les_stats_du_bloc_de_fin_de_game_lcu_sont_lues_sous_leurs_noms_en_majuscules()
    {
        var stats = StatsDuParticipant.Lire("""
            { "CHAMPIONS_KILLED": 7, "NUM_DEATHS": 1, "ASSISTS": 5, "MINIONS_KILLED": 210,
              "TOTAL_DAMAGE_DEALT_TO_CHAMPIONS": 25400, "GOLD_EARNED": 13200, "VISION_SCORE": 18 }
            """);

        Assert.NotNull(stats);
        Assert.Equal((7, 1, 5), (stats.Kills, stats.Deaths, stats.Assists));
        Assert.Equal(210, stats.MinionsKilled);
        Assert.Equal(25400, stats.DamageToChampions);
        Assert.Equal(13200, stats.GoldEarned);
        Assert.Equal(18, stats.VisionScore);
    }

    [Theory]
    [InlineData("TOP", Role.Top)]
    [InlineData("MIDDLE", Role.Mid)]
    [InlineData("BOTTOM", Role.Adc)]
    [InlineData("UTILITY", Role.Support)]
    [InlineData("support", Role.Support)]
    public void La_position_riot_donne_le_role_joue(string position, Role role)
    {
        Assert.Equal(role, StatsDuParticipant.Lire($$"""{ "teamPosition": "{{position}}" }""")?.Position);
    }

    [Fact]
    public void Une_stat_d_un_autre_type_ou_une_position_inconnue_reste_vide_sans_empecher_les_autres()
    {
        var stats = StatsDuParticipant.Lire("""
            { "kills": "beaucoup", "deaths": 2.6, "assists": null, "teamPosition": "", "lane": "NONE" }
            """);

        Assert.NotNull(stats);
        Assert.Null(stats.Kills);
        Assert.Equal(3, stats.Deaths);
        Assert.Null(stats.Assists);
        Assert.Null(stats.Position);
    }

    [Fact]
    public void Une_position_vide_laisse_place_a_la_suivante()
    {
        Assert.Equal(Role.Mid, StatsDuParticipant.Lire("""{ "teamPosition": "", "individualPosition": "MIDDLE" }""")?.Position);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("[1, 2]")]
    [InlineData("{ pas du json")]
    public void Sans_objet_json_lisible_il_n_y_a_ni_stats_ni_donnees_de_match(string? json)
    {
        Assert.Null(StatsDuParticipant.Lire(json));
        Assert.Null(DonneesDuMatch.Lire(json));
    }

    [Fact]
    public void Les_donnees_du_match_lcu_donnent_la_duree_le_mode_et_la_version()
    {
        var match = DonneesDuMatch.Lire("""
            { "gameId": 123, "gameDuration": 1712, "gameMode": "CLASSIC", "gameType": "CUSTOM_GAME", "gameVersion": "14.20.628.3111" }
            """);

        Assert.Equal(new DonneesDuMatch(1712, "CLASSIC", "CUSTOM_GAME", "14.20.628.3111"), match);
    }
}
