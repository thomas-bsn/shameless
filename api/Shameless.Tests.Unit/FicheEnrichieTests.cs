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
