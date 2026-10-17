using Shameless.Application.Entities;
using Shameless.Application.Stats;

namespace Shameless.Application.DTOs.Match;

/// <summary>
/// La page d'une Game : sa place dans sa Série (numéro, format, score de la Série), ses deux Équipes
/// avec champions, stats et notes du Companion, et son vainqueur une fois terminée. C'est le lien de
/// récap posté par le bot.
/// </summary>
/// <param name="GameNumber">Son rang dans la Série, à partir de 1 (1 pour une game legacy hors Série).</param>
/// <param name="BestOf">Le format de la Série (1, 2, 3 ou 5).</param>
/// <param name="Status">« pending » tant que le résultat n'est pas saisi, puis « completed ».</param>
/// <param name="TeamAWins">Les victoires de l'Équipe A dans la Série, à ce jour.</param>
public record GameRecapResponse(
    int Id,
    int? SeriesId,
    int GameNumber,
    int BestOf,
    string Status,
    DateTime PlayedAt,
    DateTime? CompletedAt,
    string TeamASide,
    string TeamBSide,
    string? Winner,
    int TeamAWins,
    int TeamBWins,
    SeriesOutcome? SeriesOutcome,
    string? RiotMatchId,
    DonneesDuMatch? Match,
    List<GameHistoryParticipantDto> TeamA,
    List<GameHistoryParticipantDto> TeamB)
{
    /// <summary>La page de la game, chargée avec ses participants et sa Série (games et joueurs).</summary>
    public static GameRecapResponse From(CustomGame game)
    {
        var notes = NotesDeLaGame.Calculer(game);
        List<GameHistoryParticipantDto> Equipe(string equipe) => game.ParticipantsOf(equipe)
            .OrderBy(p => p.Id)
            .Select(p => GameHistoryParticipantDto.From(p, notes[p.PlayerId]))
            .ToList();
        var series = game.Series;
        return new GameRecapResponse(
            Id: game.Id,
            SeriesId: game.SeriesId,
            GameNumber: game.NumeroDansLaSerie,
            BestOf: series?.Format ?? 1,
            Status: game.Status,
            PlayedAt: game.PlayedAt,
            CompletedAt: game.CompletedAt,
            TeamASide: game.TeamASide,
            TeamBSide: game.TeamBSide,
            Winner: game.WinningTeam,
            TeamAWins: series?.TeamAWins ?? 0,
            TeamBWins: series?.TeamBWins ?? 0,
            SeriesOutcome: series?.Outcome,
            RiotMatchId: game.RiotMatchId,
            Match: DonneesDuMatch.Lire(game.RiotMatchData),
            TeamA: Equipe(Equipes.A),
            TeamB: Equipe(Equipes.B));
    }
}
