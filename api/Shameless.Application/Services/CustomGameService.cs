using Shameless.Application.DTOs.Match;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Infrastructure.Repositories;

namespace Shameless.Application.Services;

/// <summary>
/// Lecture des custom games (en attente, historique).
/// La création des Séries, de leurs games, la saisie des résultats et l'arrêt sont dans <see cref="SeriesService"/>.
/// </summary>
public class CustomGameService
{
    private readonly ICustomGameRepository _gameRepo;
    private readonly TimeProvider _clock;

    public CustomGameService(
        ICustomGameRepository gameRepo,
        TimeProvider clock)
    {
        _gameRepo = gameRepo;
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

    public async Task<List<SeriesHistoryResponse>> GetHistoryAsync(int year, int month)
    {
        var games = await _gameRepo.GetByYearMonthAsync(year, month);

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
                    Games: group.Select(g => new GameHistoryResponse(
                        Id: g.Id,
                        SeriesId: g.SeriesId,
                        PlayedAt: g.PlayedAt,
                        TeamASide: g.TeamASide,
                        TeamBSide: g.TeamBSide,
                        Winner: g.WinningTeam,
                        TeamA: g.ParticipantsOf(Equipes.A).Select(p => GameParticipantDto.From(p.Player)).ToList(),
                        TeamB: g.ParticipantsOf(Equipes.B).Select(p => GameParticipantDto.From(p.Player)).ToList()
                    )).ToList()
                );
            })
            .OrderByDescending(s => s.PlayedAt)
            .ToList();
    }
}
