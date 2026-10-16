using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.Companion;

/// <summary>Le rapport rattaché : la game terminée, son vainqueur, et le score et l'issue de la Série.</summary>
public sealed record RapportRecuResponse(
    int SeriesId,
    int GameId,
    string Winner,
    int TeamAWins,
    int TeamBWins,
    SeriesOutcome Outcome)
{
    public static RapportRecuResponse From(CustomGame game) => new(
        SeriesId: game.Series!.Id,
        GameId: game.Id,
        Winner: game.WinningTeam!,
        TeamAWins: game.Series.TeamAWins,
        TeamBWins: game.Series.TeamBWins,
        Outcome: game.Series.Outcome);
}
