using Shameless.Application.Bot;
using Shameless.Application.DTOs.Match;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Application.Stats;

namespace Shameless.Application.Services;

/// <summary>
/// Lecture des custom games (en attente, historique).
/// La création des Séries, de leurs games, la saisie des résultats et l'arrêt sont dans <see cref="SeriesService"/>.
/// </summary>
public class CustomGameService
{
    private readonly ICustomGameRepository _gameRepo;
    private readonly VotesService _votes;
    private readonly TimeProvider _clock;

    public CustomGameService(
        ICustomGameRepository gameRepo,
        VotesService votes,
        TimeProvider clock)
    {
        _gameRepo = gameRepo;
        _votes = votes;
        _clock = clock;
    }

    public async Task<List<CustomGame>> GetCurrentMonthGames()
    {
        return await _gameRepo.GetByMonthAsync(_clock.GetUtcNow().UtcDateTime);
    }

    public async Task<List<CustomGame>> GetPendingGamesAsync()
    {
        return await _gameRepo.GetPendingAsync();
    }

    /// <summary>La page d'une game, en attente ou terminée, ou null si elle n'existe pas.</summary>
    public async Task<GameRecapResponse?> GetRecapAsync(int gameId) =>
        await _gameRepo.GetRecitAsync(gameId) is { } game ? GameRecapResponse.From(game) : null;

    /// <summary>La game telle que le bot la lit, ou null si elle n'existe pas.</summary>
    public async Task<GameDuBot?> GetPourLeBotAsync(int gameId) =>
        await _gameRepo.GetRecitAsync(gameId) is { } game ? GameDuBot.From(game) : null;

    public async Task<List<SeriesHistoryResponse>> GetHistoryAsync(int year, int month)
    {
        var games = await _gameRepo.GetByYearMonthAsync(year, month);
        var votes = await _votes.GetResultatsDesGamesAsync(games);

        return games
            .GroupBy(g => g.SeriesId)
            .Select(group =>
            {
                var firstGame = group.First();
                var series = firstGame.Series;

                return new SeriesHistoryResponse(
                    SeriesId: group.Key ?? 0,
                    Format: series?.Format ?? 1,
                    PlayedAt: series?.PlayedAt ?? firstGame.PlayedAt,
                    Outcome: series?.Outcome,
                    WinningTeam: series?.WinningTeam,
                    Games: group.Select(g =>
                    {
                        var lectures = LectureDeLaGame.Calculer(g);
                        List<GameHistoryParticipantDto> Equipe(string equipe) => g.ParticipantsOf(equipe)
                            .Select(p => GameHistoryParticipantDto.From(p, lectures[p.PlayerId]))
                            .ToList();
                        return new GameHistoryResponse(
                            Id: g.Id,
                            SeriesId: g.SeriesId,
                            PlayedAt: g.PlayedAt,
                            ModifieeParAdminLe: g.ModifieeParAdminLe,
                            TeamASide: g.TeamASide,
                            TeamBSide: g.TeamBSide,
                            Winner: g.WinningTeam,
                            RiotMatchId: g.RiotMatchId,
                            Match: DonneesDuMatch.Lire(g.RiotMatchData),
                            TeamA: Equipe(Equipes.A),
                            TeamB: Equipe(Equipes.B),
                            Dofd: votes[g.Id].Dofd.Gagnants);
                    }).ToList()
                );
            })
            .OrderByDescending(s => s.PlayedAt)
            .ToList();
    }
}
