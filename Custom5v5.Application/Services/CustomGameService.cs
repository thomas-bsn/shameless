using Custom5v5.Application.DTOs.Match;
using Custom5v5.Application.Entities;
using Custom5v5.Application.Interfaces;
using Custom5v5.Infrastructure.Repositories;

namespace Custom5v5.Application.Services;

/// <summary>
/// Service responsable de la gestion des custom games
/// SRP: Enregistrement et récupération des games uniquement
/// </summary>
public class CustomGameService
{
    private readonly ICustomGameRepository _gameRepo;
    private readonly ICustomGameParticipantRepository _participantRepo;
    private readonly IPlayerRepository _playerRepo;
    private readonly CostCalculationService _costService;
    private readonly IBestOfSeriesRepository _seriesRepo;

    public CustomGameService(
        ICustomGameRepository gameRepo,
        ICustomGameParticipantRepository participantRepo,
        IPlayerRepository playerRepo,
        CostCalculationService costService,
        IBestOfSeriesRepository seriesRepo)
    {
        _gameRepo = gameRepo;
        _participantRepo = participantRepo;
        _playerRepo = playerRepo;
        _costService = costService;
        _seriesRepo = seriesRepo;
    }
    
    public async Task<bool> AbandonSeriesAsync(int seriesId)
    {
        var games = await _gameRepo.GetBySeriesIdAsync(seriesId);
        if (!games.Any()) return false;

        foreach (var game in games)
            await _participantRepo.DeleteByGameIdAsync(game.Id);

        await _gameRepo.DeleteBySeriesIdAsync(seriesId);
        await _seriesRepo.DeleteAsync(seriesId);

        return true;
    }

    public async Task<(CustomGame game, int? seriesId)> RecordCustomGame(
        List<int> blueTeamPlayerIds,
        List<int> redTeamPlayerIds,
        DateTime? playedAt = null,
        int? seriesId = null,
        int? boFormat = null)
    {
        if (seriesId == null && boFormat != null)
        {
            var series = await _seriesRepo.AddAsync(new BestOfSeries
            {
                PlayedAt = playedAt ?? DateTime.UtcNow,
                Format = boFormat.Value
            });
            seriesId = series.Id;
        }

        var game = new CustomGame
        {
            PlayedAt = playedAt ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            SeriesId = seriesId
        };

        game = await _gameRepo.AddAsync(game);
        var participants = CreateParticipants(game.Id, blueTeamPlayerIds, redTeamPlayerIds);
        await _participantRepo.AddRangeAsync(participants);

        return (game, seriesId);
    }
    
    public async Task<object?> GetSeriesStatusAsync(int seriesId)
    {
        var series = await _seriesRepo.GetByIdAsync(seriesId);
        if (series == null) return null;

        var games = await _gameRepo.GetBySeriesIdAsync(seriesId);
        var completed = games.Where(g => g.Status == "completed").ToList();

        var blueWins = completed.Count(g => g.Winner == "blue");
        var redWins  = completed.Count(g => g.Winner == "red");

        var lastGame = games.LastOrDefault();

        return new
        {
            seriesId = series.Id,
            format = series.Format,
            blueWins,
            redWins,
            lastGame = lastGame == null ? null : new
            {
                id = lastGame.Id,
                winner = lastGame.Winner,
                blueTeam = lastGame.Participants
                    .Where(p => p.Team == "blue")
                    .Select(p => new { prenom = p.Player.Prenom, riotId = p.Player.RiotId, rankTier = p.Player.RankTier, rankDivision = p.Player.RankDivision }),
                redTeam = lastGame.Participants
                    .Where(p => p.Team == "red")
                    .Select(p => new { prenom = p.Player.Prenom, riotId = p.Player.RiotId, rankTier = p.Player.RankTier, rankDivision = p.Player.RankDivision }),
            }
        };
    }

    public async Task<List<CustomGame>> GetCurrentMonthGames()
    {
        return await _gameRepo.GetByMonthAsync(DateTime.UtcNow);
    }
    
    public async Task<List<object>> GetActiveSeriesAsync()
    {
        // Récupère toutes les séries qui ont au moins une game
        var allGames = await _gameRepo.GetAllWithSeriesAsync();
    
        var activeSeries = allGames
            .Where(g => g.SeriesId != null)
            .GroupBy(g => g.SeriesId!.Value)
            .Where(group =>
            {
                var series = group.First().Series;
                if (series == null) return false;
            
                var blueWins = group.Count(g => g.Status == "completed" && g.Winner == "blue");
                var redWins  = group.Count(g => g.Status == "completed" && g.Winner == "red");
                var needed   = (series.Format / 2) + 1;
            
                // Série active si : pas encore de vainqueur
                return blueWins < needed && redWins < needed;
            })
            .Select(g => g.Key)
            .ToList();

        var result = new List<object>();
        foreach (var seriesId in activeSeries)
        {
            var status = await GetSeriesStatusAsync(seriesId);
            if (status != null) result.Add(status);
        }
        return result;
    }
    
    public async Task<List<CustomGame>> GetPendingGamesAsync()
    {
        return await _gameRepo.GetPendingAsync();
    }
    
    public async Task<CustomGame?> SetGameResultAsync(int gameId, string winner)
    {
        var game = await _gameRepo.GetByIdAsync(gameId);
        if (game == null) return null;

        game.Winner = winner;
        game.Status = "completed";

        await _gameRepo.SaveChangesAsync();
        await _costService.RecalculateCosts(gameId);

        return game;
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
                    Games: group.Select(g => new GameHistoryResponse(
                        Id: g.Id,
                        SeriesId: g.SeriesId,
                        PlayedAt: g.PlayedAt,
                        Winner: g.Winner,
                        BlueTeam: g.Participants
                            .Where(p => p.Team == "blue")
                            .Select(p => new GameParticipantDto(p.Player.Prenom, p.Player.RiotId, p.Player.RankTier, p.Player.RankDivision, p.Player.CurrentCost))
                            .ToList(),
                        RedTeam: g.Participants
                            .Where(p => p.Team == "red")
                            .Select(p => new GameParticipantDto(p.Player.Prenom, p.Player.RiotId, p.Player.RankTier, p.Player.RankDivision, p.Player.CurrentCost))
                            .ToList()
                    )).ToList()
                );
            })
            .OrderByDescending(s => s.PlayedAt)
            .ToList();
    }

    private List<CustomGameParticipant> CreateParticipants(
        int gameId,
        List<int> blueTeamIds,
        List<int> redTeamIds
        )
    {
        var participants = new List<CustomGameParticipant>();
        
        foreach (var playerId in blueTeamIds)
        {
            participants.Add(new CustomGameParticipant
            {
                CustomGameId = gameId,
                PlayerId = playerId,
                Team = "blue"
            });
        }
        
        foreach (var playerId in redTeamIds)
        {
            participants.Add(new CustomGameParticipant
            {
                CustomGameId = gameId,
                PlayerId = playerId,
                Team = "red"
            });
        }
        
        return participants;
    }
}