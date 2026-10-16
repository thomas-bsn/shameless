using Shameless.Application.Entities;
using Shameless.Application.Services;

namespace Shameless.Api.Models.Responses.Companion;

/// <summary>
/// Le rapport traité : accepté (la game est terminée, avec son vainqueur), en revue ou rejeté (la game
/// attend toujours son résultat, winner null), et le score et l'issue de la Série.
/// </summary>
public sealed record RapportRecuResponse(
    int SeriesId,
    int GameId,
    StatutRapport Statut,
    string? Winner,
    int TeamAWins,
    int TeamBWins,
    SeriesOutcome Outcome)
{
    public static RapportRecuResponse From(RapportTraite traite) => new(
        SeriesId: traite.Game.Series!.Id,
        GameId: traite.Game.Id,
        Statut: traite.Statut,
        Winner: traite.Game.WinningTeam,
        TeamAWins: traite.Game.Series.TeamAWins,
        TeamBWins: traite.Game.Series.TeamBWins,
        Outcome: traite.Game.Series.Outcome);
}
