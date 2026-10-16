using Shameless.Application.Entities;
using Shameless.Application.Picker;
using Shameless.Application.Stats;

namespace Shameless.Tests.Unit;

/// <summary>
/// La note Shameless de performance (0 à 100) d'un Joueur pour une Game : chaque critère est noté
/// par rapport aux références du rôle joué, puis pondéré selon ce rôle (voir ADR 0011).
/// </summary>
public class NoteDePerformanceTests
{
    private const int TrenteMinutes = 1800;

    private static StatsDuParticipant Stats(
        int? kills = null, int? deaths = null, int? assists = null, int? minions = null, int? neutres = null,
        int? degats = null, int? vision = null, int? objectifs = null, Role? position = null) =>
        new(kills, deaths, assists, minions, neutres, degats, DamageTaken: null, GoldEarned: null, vision,
            WardsPlaced: null, WardsKilled: null, ControlWardsBought: null, objectifs, TurretKills: null,
            DragonKills: null, BaronKills: null, ChampLevel: null, position);

    /// <summary>Un mid de 30 minutes pile à la référence « moyenne » du rôle sur chaque critère.</summary>
    private static StatsDuParticipant MidMoyen() => Stats(
        kills: 5, deaths: 4, assists: 7, // KDA 3 ; 12 des 24 kills de l'équipe : KP 50 %
        minions: 190, neutres: 20,       // 7 CS/min
        degats: 21000,                   // 700 dégâts/min
        vision: 24,                      // 0,8 vision/min
        objectifs: 9000);                // 300 dégâts aux objectifs/min

    [Fact]
    public void Un_mid_a_la_reference_moyenne_de_son_role_sur_chaque_critere_a_50()
    {
        var note = NoteDePerformance.Calculer(MidMoyen(), RoleJoue.Determiner(Role.Mid), killsDeLEquipe: 24, dureeSecondes: TrenteMinutes);

        Assert.NotNull(note);
        Assert.Equal(50, note.Note);
        Assert.Equal(6, note.Composantes.Count);
        Assert.All(note.Composantes, c => Assert.Equal(50, c.Score));
    }

    [Fact]
    public void Entre_deux_references_le_score_est_lineaire_et_seuls_les_criteres_mesurables_comptent_avec_leurs_poids_renormalises()
    {
        // Sans durée de game : seuls le KDA et la participation aux kills sont mesurables.
        var stats = Stats(kills: 4, deaths: 2, assists: 5);

        var note = NoteDePerformance.Calculer(stats, RoleJoue.Determiner(Role.Mid), killsDeLEquipe: 20, dureeSecondes: null);

        Assert.NotNull(note);
        Assert.Collection(note.Composantes,
            kda =>
            {
                Assert.Equal(CritereDeNote.Kda, kda.Critere);
                Assert.Equal(4.5, kda.Valeur);
                Assert.Equal(75, kda.Score); // à mi-chemin entre Moyen (3) et Excellent (6)
                Assert.Equal(0.5, kda.Poids);
            },
            kp =>
            {
                Assert.Equal(CritereDeNote.ParticipationAuxKills, kp.Critere);
                Assert.Equal(0.45, kp.Valeur);
                Assert.Equal(38, kp.Score); // 37,5 : aux trois quarts entre Faible (30 %) et Moyen (50 %)
                Assert.Equal(0.5, kp.Poids);
            });
        Assert.Equal(56, note.Note); // (75 × 20 + 37,5 × 20) / 40 = 56,25
    }

    [Fact]
    public void La_note_reste_entre_0_et_100_quelles_que_soient_les_stats()
    {
        var ecrasante = Stats(kills: 20, deaths: 0, assists: 15, minions: 400, neutres: 50, degats: 60000, vision: 80, objectifs: 30000);
        var desastreuse = Stats(kills: 0, deaths: 12, assists: 1, minions: 40, neutres: 0, degats: 3000, vision: 2, objectifs: 0);

        var haute = NoteDePerformance.Calculer(ecrasante, RoleJoue.Determiner(Role.Mid), killsDeLEquipe: 35, dureeSecondes: TrenteMinutes);
        var basse = NoteDePerformance.Calculer(desastreuse, RoleJoue.Determiner(Role.Mid), killsDeLEquipe: 20, dureeSecondes: TrenteMinutes);

        Assert.Equal(100, haute?.Note);
        Assert.All(haute!.Composantes, c => Assert.Equal(100, c.Score));
        Assert.Equal(0, basse?.Note);
        Assert.All(basse!.Composantes, c => Assert.Equal(0, c.Score));
    }

    [Fact]
    public void Les_memes_stats_sont_notees_selon_les_poids_et_references_du_role_joue()
    {
        var note = NoteDePerformance.Calculer(MidMoyen(), RoleJoue.Determiner(Role.Support), killsDeLEquipe: 24, dureeSecondes: TrenteMinutes);

        Assert.NotNull(note);
        Assert.Equal(Role.Support, note.Role);
        // Le support n'est noté ni sur le CS ni sur les objectifs.
        Assert.Equal(
            [CritereDeNote.Kda, CritereDeNote.ParticipationAuxKills, CritereDeNote.DegatsParMinute, CritereDeNote.VisionParMinute],
            note.Composantes.Select(c => c.Critere));
        Assert.Equal([38, 25, 100, 0], note.Composantes.Select(c => c.Score));
        Assert.Equal([0.25, 0.3, 0.1, 0.35], note.Composantes.Select(c => c.Poids));
        Assert.Equal(27, note.Note); // (37,5 × 25 + 25 × 30 + 100 × 10 + 0 × 35) / 100 = 26,875
    }

    [Fact]
    public void Sans_role_connu_tous_les_criteres_comptent_autant()
    {
        var note = NoteDePerformance.Calculer(MidMoyen(), RoleJoue.Determiner(null), killsDeLEquipe: 24, dureeSecondes: TrenteMinutes);

        Assert.NotNull(note);
        Assert.Null(note.Role);
        Assert.Equal(SourceDuRole.Inconnue, note.SourceDuRole);
        Assert.Equal(6, note.Composantes.Count);
        Assert.All(note.Composantes, c => Assert.Equal(0.167, c.Poids));
        Assert.Equal(50, note.Note);
    }

    [Fact]
    public void Sans_mort_le_kda_vaut_kills_plus_assists_et_sans_kill_d_equipe_la_participation_est_omise()
    {
        var note = NoteDePerformance.Calculer(Stats(kills: 0, deaths: 0, assists: 2), RoleJoue.Determiner(Role.Top), killsDeLEquipe: 0, dureeSecondes: TrenteMinutes);

        var kda = Assert.Single(note!.Composantes);
        Assert.Equal(CritereDeNote.Kda, kda.Critere);
        Assert.Equal(2, kda.Valeur);
        Assert.Equal(1.0, kda.Poids);
        Assert.Equal(33, note.Note); // 2 entre Faible (1) et Moyen (2,5) pour un top : 33,3
    }

    [Fact]
    public void Sans_aucun_critere_mesurable_il_n_y_a_pas_de_note()
    {
        Assert.Null(NoteDePerformance.Calculer(Stats(kills: 3), RoleJoue.Determiner(Role.Mid), killsDeLEquipe: null, dureeSecondes: null));
        Assert.Null(NoteDePerformance.Calculer(Stats(minions: 150), RoleJoue.Determiner(Role.Mid), killsDeLEquipe: 20, dureeSecondes: null));
    }

    [Fact]
    public void Dans_une_game_la_participation_compte_les_kills_du_side_du_joueur_et_l_autorole_remplace_une_position_absente()
    {
        var serie = new BestOfSeries { Players = { new SeriesPlayer { PlayerId = 1, Role = Role.Support } } };
        CustomGameParticipant Participant(int playerId, string side, int kills) => new()
        {
            PlayerId = playerId, Side = side,
            Player = new Player { Id = playerId, MainRole = "TOP" },
            RiotParticipantData = $$"""{ "kills": {{kills}}, "deaths": 1, "assists": 2 }"""
        };
        var game = new CustomGame
        {
            Series = serie,
            Participants = { Participant(1, "blue", 2), Participant(2, "blue", 6), Participant(3, "red", 30) }
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
    public void Le_role_note_est_la_position_de_la_partie_sinon_celui_de_l_autorole_sinon_le_role_principal()
    {
        Assert.Equal(new RoleJoue(Role.Jungle, SourceDuRole.Partie), RoleJoue.Determiner(Role.Jungle, Role.Top, Role.Mid));
        Assert.Equal(new RoleJoue(Role.Top, SourceDuRole.Autorole), RoleJoue.Determiner(null, Role.Top, Role.Mid));
        Assert.Equal(new RoleJoue(Role.Mid, SourceDuRole.RolePrincipal), RoleJoue.Determiner(null, null, Role.Mid));
        Assert.Equal(new RoleJoue(null, SourceDuRole.Inconnue), RoleJoue.Determiner(null, null, null));
    }
}
