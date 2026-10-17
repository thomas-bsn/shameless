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
        CreatedAt = Lancement,
        CompletedAt = Lancement.AddMinutes(30),
        Status = GameStatuses.Completed,
        WinningTeam = Equipes.A,
    };

    private static ResultatsDesVotes VotesClos(List<int> mvp, List<int> dofd) => new(
        7, FenetreDeVote.Clos, null, null, 0, new ResultatDuTitre(mvp, []), new ResultatDuTitre(dofd, []));

    private static Pronostic Pronostic(long discordId, DateTime le, int? mvp = null, string? equipe = null) => new()
    {
        PronostiqueurDiscordId = discordId,
        PronostiqueLe = le,
        EquipeGagnante = equipe,
        MvpPlayerId = mvp,
        Mvp = mvp is { } id ? new Player { Id = id, Prenom = $"J{id}" } : null,
    };

    [Fact]
    public void Un_pronostic_depose_apres_le_lancement_de_la_game_est_ignore()
    {
        var evalues = PronosticsDeLaGame.Evaluer(
            GameTerminee(),
            [Pronostic(1, Lancement, equipe: "A"), Pronostic(2, Lancement.AddSeconds(1), equipe: "A")],
            VotesClos([], []));

        var seul = Assert.Single(evalues.Pronostics);
        Assert.Equal("1", seul.PronostiqueurDiscordId);
    }

    [Fact]
    public void Sans_mvp_elu_le_pronostic_de_mvp_est_faux()
    {
        var evalues = PronosticsDeLaGame.Evaluer(GameTerminee(), [Pronostic(1, Lancement, mvp: 3)], VotesClos([], []));

        Assert.Equal(new JoueurPronostique(3, "J3", false), evalues.Pronostics[0].Mvp);
    }
}
