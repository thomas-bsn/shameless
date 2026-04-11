using Custom5v5.Api.Contracts.Matches;

namespace Custom5v5.Api.Controllers;

using Custom5v5.Application.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("game")]
public class GameController : ControllerBase
{
    private readonly CustomGameService _gameService;
    private readonly CostCalculationService _costService;

    public GameController(CustomGameService gameService, CostCalculationService costService)
    {
        _gameService = gameService;
        _costService = costService;
    }

    [HttpPost("create")]
    public async Task<IActionResult> PostGame([FromBody] RecordGameRequest request)
    {
        var game = await _gameService.RecordCustomGame(
            blueTeamPlayerIds: request.BlueTeam,
            redTeamPlayerIds: request.RedTeam,
            playedAt: DateTime.UtcNow
        );

        return Ok(new
        {
            message = "Game enregistrée !",
            gameId = game.Id,
            playedAt = game.PlayedAt
        });
    }
    
    [HttpPut("{id}/result")]
    public async Task<IActionResult> SetResult(int id, [FromBody] SetGameResultRequest request)
    {
        var game = await _gameService.SetGameResultAsync(id, request.Winner);
        if (game == null) return NotFound();

        return Ok(new
        {
            gameId = game.Id,
            winner = game.Winner,
            status = game.Status
        });
    }

    [HttpGet("player-cost/{playerId}")]
    public async Task<IActionResult> GetPlayerCost(int playerId)
    {
        var (name, cost, mmr, baseMMR, winrateAdj) = await _costService.CalculatePlayerCostPreview(playerId);

        return Ok(new
        {
            playerId,
            name,
            cost,
            tier = MMRCalculationService.GetTierLabel(cost),
            mmr,
            baseMMR,
            winrateAdjustment = winrateAdj
        });
    }
    
    [HttpGet("pending-games")]
    public async Task<IActionResult> GetPendingGames()
    {
        var pendingGames = await _gameService.GetPendingGamesAsync();

        var result = pendingGames.Select(g => new
        {
            id = g.Id,
            playedAt = g.PlayedAt,
            status = g.Status,
            participants = g.Participants.Select(p => new
            {
                playerId = p.PlayerId,
                team = p.Team,
                prenom = p.Player.Prenom
            })
        });

        return Ok(new { pendingGames = result });
    }
}