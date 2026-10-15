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
        BestOfSeries? series = null;
    
        if (seriesId == null && boFormat != null)
        {
            series = await _seriesRepo.AddAsync(new BestOfSeries
            {
                PlayedAt = playedAt ?? DateTime.UtcNow,
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
            PlayedAt = playedAt ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            SeriesId = seriesId,
            TeamASide = series == null
                ? Sides.Blue
                : await TeamASideAsync(series.Id, blueTeamPlayerIds, redTeamPlayerIds)
        };

        game = await _gameRepo.AddAsync(game);
        var participants = CreateParticipants(game.Id, blueTeamPlayerIds, redTeamPlayerIds);
        await _participantRepo.AddRangeAsync(participants);

        return (game, seriesId);
    }

    /// <summary>
    /// L'Équipe A est le côté bleu de la première game d'une Série. Ensuite, c'est le Side
    /// où l'on retrouve le plus de joueurs de l'Équipe A de la game précédente.
    /// </summary>
    private async Task<string> TeamASideAsync(int seriesId, List<int> bluePlayerIds, List<int> redPlayerIds)
    {
        var previous = (await _gameRepo.GetBySeriesIdAsync(seriesId)).LastOrDefault();
        if (previous == null) return Sides.Blue;

        var teamA = previous.ParticipantsOf(Equipes.A).Select(p => p.PlayerId).ToHashSet();
        return redPlayerIds.Count(teamA.Contains) > bluePlayerIds.Count(teamA.Contains)
            ? Sides.Red
            : Sides.Blue;
    }

    public async Task<SeriesResponse?> GetSeriesStatusAsync(int seriesId)
    {
        var series = await _seriesRepo.GetByIdAsync(seriesId);
        if (series == null) return null;

        var games = await _gameRepo.GetBySeriesIdAsync(seriesId);
        var lastGame = games.LastOrDefault();

        return new SeriesResponse(
            SeriesId: series.Id,
            Format: series.Format,
            TeamAWins: series.TeamAWins,
            TeamBWins: series.TeamBWins,
            TeamA: lastGame == null ? [] : ToDtos(lastGame.ParticipantsOf(Equipes.A)),
            TeamB: lastGame == null ? [] : ToDtos(lastGame.ParticipantsOf(Equipes.B)),
            Games: games.Select(g => new SeriesGameResponse(
                Id: g.Id,
                Status: g.Status,
                TeamASide: g.TeamASide,
                TeamBSide: g.TeamBSide,
                Winner: g.WinningTeam,
                TeamA: ToDtos(g.ParticipantsOf(Equipes.A)),
                TeamB: ToDtos(g.ParticipantsOf(Equipes.B))
            )).ToList()
        );
    }

    private static List<GameParticipantDto> ToDtos(IEnumerable<CustomGameParticipant> participants) =>
        participants
            .Select(p => new GameParticipantDto(p.Player.Prenom, p.Player.RiotId, p.Player.RankTier, p.Player.RankDivision, p.Player.CurrentCost))
            .ToList();

    public async Task<List<CustomGame>> GetCurrentMonthGames()
    {
        return await _gameRepo.GetByMonthAsync(DateTime.UtcNow);
    }
    
    public async Task<List<SeriesResponse>> GetActiveSeriesAsync()
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

        var result = new List<SeriesResponse>();
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
    
    /// <summary>Saisit l'Équipe gagnante ("A" ou "B") d'une game.</summary>
    public async Task<CustomGame?> SetGameResultAsync(int gameId, string winningTeam)
    {
        var game = await _gameRepo.GetByIdWithParticipantsAsync(gameId);
        if (game == null) return null;

        game.WinningTeam = winningTeam;
        game.Status = "completed";
        await _gameRepo.SaveChangesAsync();

        if (game.SeriesId != null)
        {
            var series = await _seriesRepo.GetByIdAsync(game.SeriesId.Value);
            if (series != null)
            {
                var completed = (await _gameRepo.GetBySeriesIdAsync(series.Id))
                    .Where(g => g.Status == "completed")
                    .ToList();
                series.TeamAWins = completed.Count(g => g.WinningTeam == Equipes.A);
                series.TeamBWins = completed.Count(g => g.WinningTeam == Equipes.B);
                await _seriesRepo.SaveChangesAsync();
            }
        }

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
                        TeamASide: g.TeamASide,
                        TeamBSide: g.TeamBSide,
                        Winner: g.WinningTeam,
                        TeamA: ToDtos(g.ParticipantsOf(Equipes.A)),
                        TeamB: ToDtos(g.ParticipantsOf(Equipes.B))
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