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
        string winner,
        DateTime? playedAt = null)
    {
        // Validation
        ValidateTeams(blueTeamPlayerIds, redTeamPlayerIds, winner);
        
        // Vérifier que tous les joueurs existent
        var allPlayerIds = blueTeamPlayerIds.Concat(redTeamPlayerIds).ToList();
        var existingPlayers = await _playerRepo.GetByIdsAsync(allPlayerIds);
        
        if (existingPlayers.Count != 10)
            throw new ArgumentException("Some players do not exist in database");
        
        // Créer la game
        var game = new CustomGame
        {
            PlayedAt = playedAt ?? DateTime.UtcNow,
            Winner = winner.ToLower(),
            CreatedAt = DateTime.UtcNow
        };
        
        game = await _gameRepo.AddAsync(game);
        
        // Créer les participants
        var participants = CreateParticipants(game.Id, blueTeamPlayerIds, redTeamPlayerIds, winner);
        await _participantRepo.AddRangeAsync(participants);
        
        // Déclencher recalcul des costs
        await _costService.RecalculateCosts(game.Id);
        
        return game;
    }

    public async Task<List<CustomGame>> GetCurrentMonthGames()
    {
        return await _gameRepo.GetByMonthAsync(DateTime.UtcNow);
    }

    private void ValidateTeams(List<int> blueTeam, List<int> redTeam, string winner)
    {
        if (blueTeam.Count != 5)
            throw new ArgumentException("Blue team must have exactly 5 players");
        
        if (redTeam.Count != 5)
            throw new ArgumentException("Red team must have exactly 5 players");
        
        if (winner != "blue" && winner != "red")
            throw new ArgumentException("Winner must be 'blue' or 'red'");
    }

    private List<CustomGameParticipant> CreateParticipants(
        int gameId,
        List<int> blueTeamIds,
        List<int> redTeamIds,
        string winner)
    {
        var participants = new List<CustomGameParticipant>();
        
        foreach (var playerId in blueTeamIds)
        {
            participants.Add(new CustomGameParticipant
            {
                CustomGameId = gameId,
                PlayerId = playerId,
                Team = "blue",
                Won = winner == "blue"
            });
        }
        
        foreach (var playerId in redTeamIds)
        {
            participants.Add(new CustomGameParticipant
            {
                CustomGameId = gameId,
                PlayerId = playerId,
                Team = "red",
                Won = winner == "red"
            });
        }
        
        return participants;
    }
}