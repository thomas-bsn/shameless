using Shameless.Application.DTOs.Match;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Infrastructure.Repositories;

namespace Shameless.Application.Services;

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
    private readonly TimeProvider _clock;

    public CustomGameService(
        ICustomGameRepository gameRepo,
        ICustomGameParticipantRepository participantRepo,
        IPlayerRepository playerRepo,
        CostCalculationService costService,
        IBestOfSeriesRepository seriesRepo,
        TimeProvider clock)
    {
        _gameRepo = gameRepo;
        _participantRepo = participantRepo;
        _playerRepo = playerRepo;
        _costService = costService;
        _seriesRepo = seriesRepo;
        _clock = clock;
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
        BestOfSeries? series = null;
    
        if (seriesId == null && boFormat != null)
        {
            series = await _seriesRepo.AddAsync(new BestOfSeries
            {
                PlayedAt = playedAt ?? _clock.GetUtcNow().UtcDateTime,
                Format = boFormat.Value,
                TeamAWins = 0,
                TeamBWins = 0
            });
            seriesId = series.Id;
        }
        else if (seriesId != null)
        {
            series = await _seriesRepo.GetByIdAsync(seriesId.Value);
        }

        var game = new CustomGame
        {
            PlayedAt = playedAt ?? _clock.GetUtcNow().UtcDateTime,
            CreatedAt = _clock.GetUtcNow().UtcDateTime,
            SeriesId = seriesId
        };

        game = await _gameRepo.AddAsync(game);
        var participants = CreateParticipants(game.Id, blueTeamPlayerIds, redTeamPlayerIds);
        await _participantRepo.AddRangeAsync(participants);

        if (series != null && string.IsNullOrEmpty(series.TeamAReference))
        {
            var bluePlayers = await _playerRepo.GetByIdsAsync(blueTeamPlayerIds);
            var teamARiotIds = bluePlayers.Select(p => p.RiotId).ToList();
            series.TeamAReference = System.Text.Json.JsonSerializer.Serialize(teamARiotIds);
            await _seriesRepo.SaveChangesAsync();
        }

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

        List<string>? teamAReference = null;
        if (!string.IsNullOrEmpty(series.TeamAReference))
        {
            teamAReference = System.Text.Json.JsonSerializer.Deserialize<List<string>>(series.TeamAReference);
        }

        return new
        {
            seriesId = series.Id,
            format = series.Format,
            blueWins,
            redWins,
            teamAWins = series.TeamAWins,     
            teamBWins = series.TeamBWins,    
            teamAReference = teamAReference,  
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
        return await _gameRepo.GetByMonthAsync(_clock.GetUtcNow().UtcDateTime);
    }
    
    public async Task<List<object>> GetActiveSeriesAsync()
    {
        var allGames = await _gameRepo.GetAllWithSeriesAsync();

        var activeSeries = allGames
            .Where(g => g.SeriesId != null)
            .GroupBy(g => g.SeriesId!.Value)
            .Where(group =>
            {
                var series = group.First().Series;
                if (series == null) return false;
        
                var needed = (series.Format / 2) + 1;
        
                return series.TeamAWins < needed && series.TeamBWins < needed;
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
        var game = await _gameRepo.GetByIdWithParticipantsAsync(gameId);
        if (game == null) return null;

        game.Winner = winner;
        game.Status = "completed";

        if (game.SeriesId != null)
        {
            var series = await _seriesRepo.GetByIdAsync(game.SeriesId.Value);
            if (series != null && !string.IsNullOrEmpty(series.TeamAReference))
            {
                var teamARiotIds = System.Text.Json.JsonSerializer.Deserialize<List<string>>(series.TeamAReference);
            
                // Déterminer quelle équipe est sur blue side
                var blueTeamRiotIds = game.Participants
                    .Where(p => p.Team == "blue")
                    .Select(p => p.Player.RiotId)
                    .ToList();
            
                bool isTeamAOnBlue = teamARiotIds!.Any(riotId => blueTeamRiotIds.Contains(riotId));
                bool teamAWon = (winner == "blue" && isTeamAOnBlue) || (winner == "red" && !isTeamAOnBlue);
            
                if (teamAWon)
                    series.TeamAWins++;
                else
                    series.TeamBWins++;
            
                await _seriesRepo.SaveChangesAsync();
            }
        }

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
                    TeamAReference: series?.TeamAReference != null 
                        ? System.Text.Json.JsonSerializer.Deserialize<List<string>>(series.TeamAReference) 
                        : null,
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