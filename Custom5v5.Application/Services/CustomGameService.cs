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

    public CustomGameService(
        ICustomGameRepository gameRepo,
        ICustomGameParticipantRepository participantRepo,
        IPlayerRepository playerRepo,
        CostCalculationService costService)
    {
        _gameRepo = gameRepo;
        _participantRepo = participantRepo;
        _playerRepo = playerRepo;
        _costService = costService;
    }

    public async Task<CustomGame> RecordCustomGame(
        List<int> blueTeamPlayerIds,
        List<int> redTeamPlayerIds,
        DateTime? playedAt = null)
    {
        // Créer la game
        var game = new CustomGame
        {
            PlayedAt = playedAt ?? DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        
        game = await _gameRepo.AddAsync(game);
        
        // Créer les participants
        var participants = CreateParticipants(game.Id, blueTeamPlayerIds, redTeamPlayerIds);
        await _participantRepo.AddRangeAsync(participants);
        
        return game;
    }

    public async Task<List<CustomGame>> GetCurrentMonthGames()
    {
        return await _gameRepo.GetByMonthAsync(DateTime.UtcNow);
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