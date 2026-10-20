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
/// <param name="ModifieeParAdminLe">Quand un Administrateur a corrigé la game ; null si elle est telle que saisie.</param>
/// <param name="Status">« pending » tant que le résultat n'est pas saisi, puis « completed ».</param>
/// <param name="TeamAObjectifs">Les objectifs de l'Équipe A (dragons, barons, tourelles, premier sang…) lus dans le Rapport ; null sans Rapport.</param>
/// <param name="TeamBObjectifs">Les objectifs de l'Équipe B ; null sans Rapport.</param>
/// <param name="TeamAWins">Les victoires de l'Équipe A dans la Série, à ce jour.</param>
public record GameRecapResponse(
    int Id,
    int? SeriesId,
    int GameNumber,
    int BestOf,
    string Status,
    DateTime PlayedAt,
    DateTime? CompletedAt,
    DateTime? ModifieeParAdminLe,
    string TeamASide,
    string TeamBSide,
    string? Winner,
    int TeamAWins,
    int TeamBWins,
    SeriesOutcome? SeriesOutcome,
    string? RiotMatchId,
    DonneesDuMatch? Match,
    ObjectifsDEquipe? TeamAObjectifs,
    ObjectifsDEquipe? TeamBObjectifs,
    List<GameHistoryParticipantDto> TeamA,
    List<GameHistoryParticipantDto> TeamB)
{
    /// <summary>La page de la game, chargée avec ses participants et sa Série (games et joueurs).</summary>
    public static GameRecapResponse From(CustomGame game)
    {
        var lectures = LectureDeLaGame.Calculer(game);
        List<GameHistoryParticipantDto> Equipe(string equipe) => game.ParticipantsOf(equipe)
            .OrderBy(p => p.Id)
            .Select(p => GameHistoryParticipantDto.From(p, lectures[p.PlayerId]))
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
            ModifieeParAdminLe: game.ModifieeParAdminLe,
            TeamASide: game.TeamASide,
            TeamBSide: game.TeamBSide,
            Winner: game.WinningTeam,
            TeamAWins: series?.TeamAWins ?? 0,
            TeamBWins: series?.TeamBWins ?? 0,
            SeriesOutcome: series?.Outcome,
            RiotMatchId: game.RiotMatchId,
            Match: DonneesDuMatch.Lire(game.RiotMatchData),
            TeamAObjectifs: ObjectifsDEquipe.Lire(game.RiotMatchData, game.TeamASide),
            TeamBObjectifs: ObjectifsDEquipe.Lire(game.RiotMatchData, game.TeamBSide),
            TeamA: Equipe(Equipes.A),
            TeamB: Equipe(Equipes.B));
    }
}
