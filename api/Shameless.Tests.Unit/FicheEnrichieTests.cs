using Shameless.Application.Entities;
using Shameless.Application.Joueurs;

namespace Shameless.Tests.Unit;

/// <summary>Le regroupement par jour de la liste des games de la fiche : le jour est celui du fuseau de l'Instance (ADR 0014), pas le jour UTC.</summary>
public class FicheEnrichieTests
{
    private static readonly TimeZoneInfo Paris = TimeZoneInfo.FindSystemTimeZoneById("Europe/Paris");

    private static CustomGame Game(int id, DateTime playedAtUtc)
    {
        var game = new CustomGame
        {
            Id = id,
            Status = GameStatuses.Completed,
            PlayedAt = DateTime.SpecifyKind(playedAtUtc, DateTimeKind.Utc)
        };
        game.Participants.Add(new CustomGameParticipant
        {
            PlayerId = 1, Side = "blue", CustomGame = game, Player = new Player { Id = 1, MainRole = "TOP" }
        });
        return game;
    }

    private static List<(string? Jour, int Games)> Jours(TimeZoneInfo? fuseau)
    {
        // 00 h 30 et 09 h 00 à Paris le 21 octobre (22 h 30 UTC le 20, 07 h 00 UTC le 21) : un seul jour local.
        var games = new[] { Game(1, new DateTime(2026, 10, 20, 22, 30, 0)), Game(2, new DateTime(2026, 10, 21, 7, 0, 0)) };
        var fiche = FicheJoueur.Calculer(new Player { Id = 1, Prenom = "A1" }, games, null,
            new FiltreDeGames(Regroupement.Jour), fuseau);
        return fiche.Liste.Select(g => (g.Jour, g.Games.Count)).ToList();
    }

    [Fact]
    public void Le_regroupement_par_jour_suit_le_jour_dans_le_fuseau_de_l_Instance()
    {
        Assert.Equal([("2026-10-21", 2)], Jours(Paris));
    }

    [Fact]
    public void Sans_fuseau_le_regroupement_par_jour_reste_en_UTC()
    {
        Assert.Equal([("2026-10-21", 1), ("2026-10-20", 1)], Jours(null));
    }
}

/// <summary>Ce que la ligne de game de la fiche ajoute : l'adversaire de la voie et la participation aux kills.</summary>
public class LigneDeGameDeLaFicheTests
{
    private static CustomGameParticipant Participant(CustomGame game, int id, string side, string champion, string position, int kills, int assists) => new()
    {
        PlayerId = id, Side = side, ChampionId = champion, CustomGame = game,
        Player = new Player { Id = id, Prenom = $"J{id}", MainRole = "TOP" },
        RiotParticipantData = $$"""{ "kills": {{kills}}, "assists": {{assists}}, "deaths": 1, "teamPosition": "{{position}}" }"""
    };

    private static GameDeLaFiche Ligne()
    {
        var game = new CustomGame { Id = 1, Status = GameStatuses.Completed, PlayedAt = DateTime.UtcNow };
        game.Participants.Add(Participant(game, 1, "blue", "Zed", "MIDDLE", 8, 2));
        game.Participants.Add(Participant(game, 2, "blue", "Ornn", "TOP", 2, 0));
        game.Participants.Add(Participant(game, 3, "red", "Ahri", "MIDDLE", 3, 3));
        game.Participants.Add(Participant(game, 4, "red", "Fiora", "TOP", 1, 1));
        return FicheJoueur.Calculer(new Player { Id = 1, Prenom = "J1" }, [game]).Games.Single();
    }

    [Fact]
    public void L_adversaire_de_la_voie_est_le_champion_adverse_du_meme_poste()
    {
        Assert.Equal("Ahri", Ligne().AdversaireChampionId);
    }

    [Fact]
    public void La_participation_aux_kills_est_en_pourcentage_des_kills_de_l_equipe()
    {
        // (8 kills + 2 assists) / 10 kills d'équipe
        Assert.Equal(100.0, Ligne().ParticipationAuxKills);
    }
}
