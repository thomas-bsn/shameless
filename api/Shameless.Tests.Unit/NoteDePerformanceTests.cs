using Shameless.Application.Entities;
using Shameless.Application.Picker;
using Shameless.Application.Stats;

namespace Shameless.Tests.Unit;

/// <summary>
/// La note Shameless de performance (0 à 100) d'un Joueur pour une Game : chaque critère est noté
/// par rapport aux références du rôle joué, puis pondéré selon ce rôle ; la victoire compte comme une
/// composante (voir ADR 0011). Les valeurs attendues viennent des références calées de
/// <c>tools/note-calage/resultats/calage-2026-10-20-lisse.json</c>, recalculées avec l'outil de calage.
/// </summary>
public class NoteDePerformanceTests
{
    private const int TrenteMinutes = 1800;
    private const int QuaranteMinutes = 2400;

    private static StatsDuParticipant Stats(
        int? kills = null, int? deaths = null, int? assists = null, int? minions = null, int? neutres = null,
        int? degats = null, int? vision = null, int? objectifs = null, Role? position = null, int? or = null) =>
        new(kills, deaths, assists, minions, neutres, degats, DamageTaken: null, or, vision,
            WardsPlaced: null, WardsKilled: null, ControlWardsBought: null, objectifs, TurretKills: null,
            DragonKills: null, BaronKills: null, ChampLevel: null, position);

    /// <summary>
    /// Un mid de 40 minutes à la référence « moyenne » du rôle sur chaque critère, aux arrondis entiers
    /// des stats près : KDA 23/15 (1,53), CS 245 (6,13/min), 24 200 dégâts (605/min), vision 34 (0,85/min),
    /// 9 920 dégâts aux objectifs (248/min), 21 120 d'or (528/min), 16 kills.
    /// Équipe : 53 kills (KP 43 %, 30 % des kills), 100 833 dégâts (24 %), 96 000 d'or (22 %).
    /// </summary>
    private static StatsDuParticipant MidMoyen() => Stats(
        kills: 16, deaths: 15, assists: 7, minions: 225, neutres: 20, degats: 24200, vision: 34, objectifs: 9920, or: 21120);

    private static NoteDePerformance? NoterLeMidMoyen(Role? role, bool? gagnee = null) => NoteDePerformance.Calculer(
        MidMoyen(), RoleJoue.Determiner(role), killsDeLEquipe: 53, dureeSecondes: QuaranteMinutes,
        degatsDeLEquipe: 100833, orDeLEquipe: 96000, gagnee: gagnee);

    [Fact]
    public void Un_mid_a_la_reference_moyenne_de_son_role_sur_chaque_critere_a_50()
    {
        var note = NoterLeMidMoyen(Role.Mid);

        Assert.NotNull(note);
        Assert.Equal(50, note.Note);
        Assert.Equal(
            [CritereDeNote.Kda, CritereDeNote.ParticipationAuxKills, CritereDeNote.CsParMinute, CritereDeNote.DegatsParMinute,
             CritereDeNote.VisionParMinute, CritereDeNote.ObjectifsParMinute, CritereDeNote.OrParMinute,
             CritereDeNote.PartDesKills, CritereDeNote.PartDesDegats, CritereDeNote.PartDeLOr],
            note.Composantes.Select(c => c.Critere));
        Assert.All(note.Composantes, c => Assert.InRange(c.Score, 49, 51)); // stats entières : à moins d'un point de 50
    }

    [Fact]
    public void La_victoire_est_une_composante_qui_vaut_100_si_la_game_est_gagnee_et_0_sinon_avec_le_poids_du_role()
    {
        var gagnee = NoterLeMidMoyen(Role.Mid, gagnee: true)!;
        var perdue = NoterLeMidMoyen(Role.Mid, gagnee: false)!;

        var victoire = gagnee.Composantes.Single(c => c.Critere == CritereDeNote.Victoire);
        Assert.Equal(1, victoire.Valeur);
        Assert.Equal(100, victoire.Score);
        Assert.Equal(0.149, victoire.Poids); // 15 sur 101 pour un mid
        Assert.Equal(57, gagnee.Note);       // (50 × 86 + 100 × 15) / 101 = 57,4
        var defaite = perdue.Composantes.Single(c => c.Critere == CritereDeNote.Victoire);
        Assert.Equal(0, defaite.Valeur);
        Assert.Equal(0, defaite.Score);
        Assert.Equal(43, perdue.Note);       // 50 × 86 / 101 = 42,6
        Assert.DoesNotContain(NoterLeMidMoyen(Role.Mid)!.Composantes, c => c.Critere == CritereDeNote.Victoire);
    }

    [Fact]
    public void Entre_deux_references_le_score_est_lineaire_et_seuls_les_criteres_mesurables_comptent_avec_leurs_poids_renormalises()
    {
        // Sans durée de game ni dégâts ni or d'équipe : KDA, participation aux kills et part des kills.
        var stats = Stats(kills: 4, deaths: 2, assists: 5);

        var note = NoteDePerformance.Calculer(stats, RoleJoue.Determiner(Role.Mid), killsDeLEquipe: 20, dureeSecondes: null);

        Assert.NotNull(note);
        Assert.Collection(note.Composantes,
            kda =>
            {
                Assert.Equal(CritereDeNote.Kda, kda.Critere);
                Assert.Equal(4.5, kda.Valeur);
                Assert.Equal(100, kda.Score); // au-delà d'Excellent (3,51)
                Assert.Equal(0.821, kda.Poids); // 23 sur 28
            },
            kp =>
            {
                Assert.Equal(CritereDeNote.ParticipationAuxKills, kp.Critere);
                Assert.Equal(0.45, kp.Valeur);
                Assert.Equal(54, kp.Score); // 54,1 : un peu au-dessus de Moyen (43,5 %), vers Excellent (61,9 %)
                Assert.Equal(0.036, kp.Poids); // 1 sur 28
            },
            part =>
            {
                Assert.Equal(CritereDeNote.PartDesKills, part.Critere);
                Assert.Equal(0.2, part.Valeur);
                Assert.Equal(33, part.Score); // 32,8 : aux deux tiers entre Faible (0) et Moyen (30,5 %)
                Assert.Equal(0.143, part.Poids); // 4 sur 28
            });
        Assert.Equal(89, note.Note); // (100 × 23 + 54,1 × 1 + 32,8 × 4) / 28 = 88,8
    }

    [Fact]
    public void La_note_reste_entre_0_et_100_quelles_que_soient_les_stats()
    {
        var ecrasante = Stats(kills: 20, deaths: 0, assists: 15, minions: 400, neutres: 50, degats: 60000, vision: 80, objectifs: 30000, or: 30000);
        // Dégâts et vision sous les valeurs Faible du mid (63 dégâts/min, 0 vision/min) pour que tout tombe à 0.
        var desastreuse = Stats(kills: 0, deaths: 12, assists: 1, minions: 40, neutres: 0, degats: 1000, vision: 0, objectifs: 0, or: 6000);

        var haute = NoteDePerformance.Calculer(ecrasante, RoleJoue.Determiner(Role.Mid), killsDeLEquipe: 35, dureeSecondes: TrenteMinutes,
            degatsDeLEquipe: 70000, orDeLEquipe: 50000, gagnee: true);
        var basse = NoteDePerformance.Calculer(desastreuse, RoleJoue.Determiner(Role.Mid), killsDeLEquipe: 20, dureeSecondes: TrenteMinutes,
            degatsDeLEquipe: 60000, orDeLEquipe: 60000, gagnee: false);

        Assert.Equal(100, haute?.Note);
        Assert.All(haute!.Composantes, c => Assert.Equal(100, c.Score));
        Assert.Equal(0, basse?.Note);
        Assert.All(basse!.Composantes, c => Assert.Equal(0, c.Score));
    }

    [Fact]
    public void Les_memes_stats_sont_notees_selon_les_poids_et_references_du_role_joue()
    {
        var note = NoterLeMidMoyen(Role.Support);

        Assert.NotNull(note);
        Assert.Equal(Role.Support, note.Role);
        // Le support n'est pas noté sur le CS.
        Assert.Equal(
            [CritereDeNote.Kda, CritereDeNote.ParticipationAuxKills, CritereDeNote.DegatsParMinute, CritereDeNote.VisionParMinute,
             CritereDeNote.ObjectifsParMinute, CritereDeNote.OrParMinute, CritereDeNote.PartDesKills, CritereDeNote.PartDesDegats,
             CritereDeNote.PartDeLOr],
            note.Composantes.Select(c => c.Critere));
        Assert.Equal([22, 58, 74, 21, 100, 100, 100, 100, 100], note.Composantes.Select(c => c.Score));
        Assert.Equal([0.258, 0.112, 0.124, 0.236, 0.045, 0.191, 0.011, 0.011, 0.011], note.Composantes.Select(c => c.Poids));
        Assert.Equal(53, note.Note); // 53,3 : le KDA et la vision tirent vers le bas, l'or et les parts de l'équipe vers le haut
    }

    [Fact]
    public void Sans_role_connu_tous_les_criteres_comptent_autant()
    {
        var note = NoterLeMidMoyen(role: null);

        Assert.NotNull(note);
        Assert.Null(note.Role);
        Assert.Equal(SourceDuRole.Inconnue, note.SourceDuRole);
        Assert.Equal(10, note.Composantes.Count);
        Assert.All(note.Composantes, c => Assert.Equal(0.1, c.Poids));
        Assert.Equal(50, note.Note);
        // La victoire compte comme un critère de plus.
        var gagnee = NoterLeMidMoyen(role: null, gagnee: true)!;
        Assert.All(gagnee.Composantes, c => Assert.Equal(0.091, c.Poids));
        Assert.Equal(54, gagnee.Note); // (499 + 100) / 11 = 54,5
    }

    [Fact]
    public void La_victoire_ne_compte_qu_avec_au_moins_quatre_autres_criteres_mesures()
    {
        var stats = Stats(kills: 10, deaths: 2, assists: 5, minions: 200);
        var role = RoleJoue.Determiner(Role.Mid);

        // Trois critères (KDA, KP, part des kills) : la victoire (jusqu'à 34 % de la note) est omise.
        var troisSansVictoire = NoteDePerformance.Calculer(stats, role, killsDeLEquipe: 20, dureeSecondes: null)!;
        var troisPerdue = NoteDePerformance.Calculer(stats, role, killsDeLEquipe: 20, dureeSecondes: null, gagnee: false)!;
        Assert.Equal(3, troisPerdue.Composantes.Count);
        Assert.DoesNotContain(troisPerdue.Composantes, c => c.Critere == CritereDeNote.Victoire);
        Assert.Equal(troisSansVictoire.Note, troisPerdue.Note);

        // Quatre critères (avec le CS par minute) : la victoire compte.
        var quatrePerdue = NoteDePerformance.Calculer(stats, role, killsDeLEquipe: 20, dureeSecondes: QuaranteMinutes, gagnee: false)!;
        Assert.Equal(5, quatrePerdue.Composantes.Count);
        Assert.Contains(quatrePerdue.Composantes, c => c.Critere == CritereDeNote.Victoire);
    }

    [Fact]
    public void Sans_mort_le_kda_vaut_kills_plus_assists_et_sans_kill_d_equipe_la_participation_est_omise()
    {
        var note = NoteDePerformance.Calculer(Stats(kills: 0, deaths: 0, assists: 2), RoleJoue.Determiner(Role.Top), killsDeLEquipe: 0, dureeSecondes: TrenteMinutes);

        var kda = Assert.Single(note!.Composantes);
        Assert.Equal(CritereDeNote.Kda, kda.Critere);
        Assert.Equal(2, kda.Valeur);
        Assert.Equal(1.0, kda.Poids);
        Assert.Equal(78, note.Note); // 2 entre Moyen (1,02) et Excellent (2,78) pour un top : 77,8
    }

    [Fact]
    public void Sans_aucun_critere_mesurable_il_n_y_a_pas_de_note()
    {
        Assert.Null(NoteDePerformance.Calculer(Stats(kills: 3), RoleJoue.Determiner(Role.Mid), killsDeLEquipe: null, dureeSecondes: null));
        Assert.Null(NoteDePerformance.Calculer(Stats(minions: 150), RoleJoue.Determiner(Role.Mid), killsDeLEquipe: 20, dureeSecondes: null));
        // Le résultat seul ne fait pas une note.
        Assert.Null(NoteDePerformance.Calculer(Stats(), RoleJoue.Determiner(Role.Mid), killsDeLEquipe: null, dureeSecondes: null, gagnee: true));
    }

    private static CustomGameParticipant Participant(int playerId, string side, string? stats) => new()
    {
        PlayerId = playerId, Side = side,
        Player = new Player { Id = playerId, MainRole = "TOP" },
        RiotParticipantData = stats
    };

    [Fact]
    public void Dans_une_game_la_participation_compte_les_kills_du_side_du_joueur_et_l_autorole_remplace_une_position_absente()
    {
        var serie = new BestOfSeries { Players = { new SeriesPlayer { PlayerId = 1, Role = Role.Support } } };
        CustomGameParticipant Joueur(int playerId, string side, int kills) =>
            Participant(playerId, side, $$"""{ "kills": {{kills}}, "deaths": 1, "assists": 2 }""");
        var game = new CustomGame
        {
            Series = serie,
            Participants = { Joueur(1, "blue", 2), Joueur(2, "blue", 6), Joueur(3, "red", 30) }
        };

        var notes = NotesDeLaGame.Calculer(game);

        var support = notes[1]!;
        Assert.Equal(new RoleJoue(Role.Support, SourceDuRole.Autorole), new RoleJoue(support.Role, support.SourceDuRole));
        Assert.Equal(0.5, support.Composantes.Single(c => c.Critere == CritereDeNote.ParticipationAuxKills).Valeur); // (2 + 2) / 8
        Assert.Equal(SourceDuRole.RolePrincipal, notes[2]!.SourceDuRole);
        Assert.Equal(Role.Top, notes[2]!.Role);
        Assert.Equal(notes[3]!.Note, NotesDeLaGame.DuJoueur(game, 3)?.Note);
        Assert.Null(NotesDeLaGame.DuJoueur(game, 99));
    }

    [Fact]
    public void Dans_une_game_terminee_la_victoire_et_les_parts_de_l_equipe_viennent_du_vainqueur_et_des_stats_du_side()
    {
        string Stats(int kills, int degats, int or) =>
            $$"""{ "kills": {{kills}}, "deaths": 2, "assists": 3, "totalDamageDealtToChampions": {{degats}}, "goldEarned": {{or}}, "teamPosition": "MIDDLE" }""";
        var game = new CustomGame
        {
            TeamASide = "blue",
            WinningTeam = Equipes.A,
            RiotMatchData = """{ "gameDuration": 1800 }""",
            Participants =
            {
                Participant(1, "blue", Stats(4, 20000, 15000)),
                Participant(2, "blue", Stats(6, 10000, 10000)),
                Participant(3, "red", Stats(8, 25000, 20000))
            }
        };

        var notes = NotesDeLaGame.Calculer(game);

        var vainqueur = notes[1]!.Composantes;
        Assert.Equal(100, vainqueur.Single(c => c.Critere == CritereDeNote.Victoire).Score);
        Assert.Equal(0.67, vainqueur.Single(c => c.Critere == CritereDeNote.PartDesDegats).Valeur); // 20 000 / 30 000
        Assert.Equal(0.6, vainqueur.Single(c => c.Critere == CritereDeNote.PartDeLOr).Valeur);      // 15 000 / 25 000
        Assert.Equal(0.4, vainqueur.Single(c => c.Critere == CritereDeNote.PartDesKills).Valeur);   // 4 / 10
        Assert.Equal(0, notes[3]!.Composantes.Single(c => c.Critere == CritereDeNote.Victoire).Score);
    }

    [Fact]
    public void Sans_vainqueur_ou_avec_un_coequipier_sans_stats_la_victoire_et_les_parts_de_l_equipe_sont_omises()
    {
        string Stats(int kills) =>
            $$"""{ "kills": {{kills}}, "deaths": 2, "assists": 3, "totalDamageDealtToChampions": 20000, "goldEarned": 15000, "teamPosition": "MIDDLE" }""";
        var game = new CustomGame
        {
            RiotMatchData = """{ "gameDuration": 1800 }""",
            Participants = { Participant(1, "blue", Stats(4)), Participant(2, "blue", stats: null) }
        };

        var criteres = NotesDeLaGame.Calculer(game)[1]!.Composantes.Select(c => c.Critere).ToList();

        Assert.DoesNotContain(CritereDeNote.Victoire, criteres);   // pas de vainqueur : game non terminée
        Assert.DoesNotContain(CritereDeNote.PartDesDegats, criteres); // les dégâts d'un coéquipier manquent
        Assert.DoesNotContain(CritereDeNote.PartDeLOr, criteres);
        Assert.Contains(CritereDeNote.OrParMinute, criteres);        // l'or du joueur seul suffit au « par minute »
        Assert.Null(NotesDeLaGame.Calculer(game)[2]);                // sans stats, pas de note
    }

    private static CustomGame GameDeDeux(Action<CustomGameParticipant>? corriger = null)
    {
        string Stats(int kills, int degats, int or) =>
            $$"""{ "kills": {{kills}}, "deaths": 2, "assists": 3, "totalDamageDealtToChampions": {{degats}}, "goldEarned": {{or}}, "teamPosition": "MIDDLE" }""";
        var game = new CustomGame
        {
            TeamASide = "blue",
            WinningTeam = Equipes.A,
            RiotMatchData = """{ "gameDuration": 1800 }""",
            Participants =
            {
                Participant(1, "blue", Stats(4, 20000, 15000)),
                Participant(2, "blue", Stats(6, 10000, 10000)),
                Participant(3, "red", Stats(8, 25000, 20000))
            }
        };
        corriger?.Invoke(game.Participants.Single(p => p.PlayerId == 2));
        return game;
    }

    [Fact]
    public void La_note_d_origine_se_calcule_sur_les_stats_d_origine_du_joueur_et_de_toute_son_equipe()
    {
        var sansCorrection = NotesDeLaGame.Calculer(GameDeDeux());
        var corrigee = GameDeDeux(p => p.Corrections.Add(new CorrectionDeStat { Champ = ChampCorrigeable.Kills, Valeur = "20" }));

        var notes = NotesDeLaGame.Calculer(corrigee);

        Assert.Equal(sansCorrection[2]!.Note, notes[2]!.NoteOrigine);
        Assert.Equal(sansCorrection[1]!.Note, notes[1]!.NoteOrigine); // le coéquipier corrigé change les totaux de l'Équipe
        Assert.NotEqual(sansCorrection[1]!.Note, notes[1]!.Note);
    }

    [Fact]
    public void Une_correction_de_champion_ou_de_note_ne_change_pas_la_note_d_origine()
    {
        var game = GameDeDeux(p => p.Corrections.Add(new CorrectionDeStat { Champ = ChampCorrigeable.Note, Valeur = "99" }));
        var sansCorrection = NotesDeLaGame.Calculer(GameDeDeux());

        var notes = NotesDeLaGame.Calculer(game);

        Assert.Equal(99, notes[2]!.Note);
        Assert.Equal(sansCorrection[2]!.Note, notes[2]!.NoteOrigine);
        Assert.Equal(sansCorrection[1]!.Note, notes[1]!.NoteOrigine);
    }

    [Fact]
    public void La_page_de_la_game_n_affiche_pas_de_part_sur_un_total_d_equipe_incomplet()
    {
        var game = GameDeDeux();
        game.Participants.Add(Participant(4, "blue", stats: null)); // un coéquipier sans stats : les totaux de dégâts et d'or sont partiels

        var details = LectureDeLaGame.Calculer(game)[1].Details;

        Assert.Null(details.PartDesDegats);
        Assert.Null(details.PartDeLOr);
        Assert.Equal(0.4, details.PartDesKills); // les kills connus : 4 / 10, comme pour la note
    }

    [Fact]
    public void Le_role_note_est_la_position_de_la_partie_sinon_celui_de_l_autorole_sinon_le_role_principal()
    {
        Assert.Equal(new RoleJoue(Role.Jungle, SourceDuRole.Partie), RoleJoue.Determiner(Role.Jungle, Role.Top, Role.Mid));
        Assert.Equal(new RoleJoue(Role.Top, SourceDuRole.Autorole), RoleJoue.Determiner(null, Role.Top, Role.Mid));
        Assert.Equal(new RoleJoue(Role.Mid, SourceDuRole.RolePrincipal), RoleJoue.Determiner(null, null, Role.Mid));
        Assert.Equal(new RoleJoue(null, SourceDuRole.Inconnue), RoleJoue.Determiner(null, null, null));
    }
}
