using Shameless.Application.Entities;
using Shameless.Application.Pronostics;
using Shameless.Application.Votes;

namespace Shameless.Tests.Unit;

/// <summary>L'évaluation des pronostics d'une Game, d'après son résultat et ses votes.</summary>
public class PronosticsTests
{
    private static readonly DateTime Lancement = new(2026, 10, 17, 20, 0, 0, DateTimeKind.Utc);

    private static CustomGame GameTerminee() => new()
    {
        Id = 7,
        CreatedAt = Lancement.AddMinutes(-3),
        StartedAt = Lancement,
        CompletedAt = Lancement.AddMinutes(30),
        Status = GameStatuses.Completed,
        WinningTeam = Equipes.A,
    };

    private static ResultatsDesVotes VotesClos(List<int> dofd) => new(
        7, FenetreDeVote.Clos, null, null, 0, new ResultatDuTitre(dofd, []));

    private static Pronostic Pronostic(long discordId, DateTime le, int? dofd = null, string? equipe = null) => new()
    {
        PronostiqueurDiscordId = discordId,
        PronostiqueLe = le,
        EquipeGagnante = equipe,
        DofdPlayerId = dofd,
        Dofd = dofd is { } id ? new Player { Id = id, Prenom = $"J{id}" } : null,
    };

    [Fact]
    public void Un_pronostic_depose_apres_le_lancement_de_la_game_est_ignore()
    {
        var evalues = PronosticsDeLaGame.Evaluer(
            GameTerminee(),
            [Pronostic(1, Lancement, equipe: "A"), Pronostic(2, Lancement.AddSeconds(1), equipe: "A")],
            VotesClos([]));

        var seul = Assert.Single(evalues.Pronostics);
        Assert.Equal("1", seul.PronostiqueurDiscordId);
    }

    [Fact]
    public void Tant_que_la_partie_n_a_pas_commence_aucun_pronostic_n_est_ignore()
    {
        var game = GameTerminee();
        game.StartedAt = null;
        game.CompletedAt = null;
        game.Status = GameStatuses.Pending;
        game.WinningTeam = null;

        var evalues = PronosticsDeLaGame.Evaluer(game, [Pronostic(1, Lancement.AddHours(1), equipe: "A")], VotesClos([]));

        Assert.Single(evalues.Pronostics);
    }

    [Fact]
    public void Une_game_terminee_sans_debut_borne_les_pronostics_a_sa_cloture()
    {
        var game = GameTerminee();
        game.StartedAt = null;

        var evalues = PronosticsDeLaGame.Evaluer(
            game,
            [Pronostic(1, Lancement.AddMinutes(30), equipe: "A"), Pronostic(2, Lancement.AddMinutes(31), equipe: "A")],
            VotesClos([]));

        Assert.Equal("1", Assert.Single(evalues.Pronostics).PronostiqueurDiscordId);
    }

    [Fact]
    public void Une_game_migree_borne_les_pronostics_a_son_lancement()
    {
        var game = GameTerminee();
        game.StartedAt = game.CreatedAt;

        var evalues = PronosticsDeLaGame.Evaluer(
            game,
            [Pronostic(1, game.CreatedAt, equipe: "A"), Pronostic(2, game.CreatedAt.AddMinutes(1), equipe: "A")],
            VotesClos([]));

        Assert.Equal("1", Assert.Single(evalues.Pronostics).PronostiqueurDiscordId);
    }

    [Fact]
    public void Sans_dofd_elu_le_pronostic_de_dofd_est_faux()
    {
        var evalues = PronosticsDeLaGame.Evaluer(GameTerminee(), [Pronostic(1, Lancement, dofd: 3)], VotesClos([]));

        Assert.Equal(new JoueurPronostique(3, "J3", false), evalues.Pronostics[0].Dofd);
    }
}
