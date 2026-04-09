namespace Custom5v5.Api.Controllers;

using Custom5v5.Application.Services;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/[controller]")]
public class TestController : ControllerBase
{
    private readonly CustomGameService _gameService;
    private readonly CostCalculationService _costService;

    public TestController(CustomGameService gameService, CostCalculationService costService)
    {
        _gameService = gameService;
        _costService = costService;
    }

    [HttpPost("record-game")]
    public async Task<IActionResult> RecordTestGame()
    {
        // Remplace avec les IDs réels de tes 10 joueurs dans ta DB
        var blueTeam = new List<int> { 8, 10, 11, 12, 13 };
        var redTeam = new List<int> { 2, 6, 34, 36, 31 }; 

        var game = await _gameService.RecordCustomGame(
            blueTeamPlayerIds: blueTeam,
            redTeamPlayerIds: redTeam,
            winner: "blue",
            playedAt: DateTime.UtcNow
        );

        return Ok(new
        {
            message = "Game enregistrée !",
            gameId = game.Id,
            playedAt = game.PlayedAt,
            winner = game.Winner
        });
    }

    [HttpGet("player-cost/{playerId}")]
    public async Task<IActionResult> GetPlayerCost(int playerId)
    {
        var (cost, mmr, baseMMR, winrateAdj) = await _costService.CalculatePlayerCostPreview(playerId);

        return Ok(new
        {
            playerId,
            cost,
            tier = MMRCalculationService.GetTierLabel(cost),
            mmr,
            baseMMR,
            winrateAdjustment = winrateAdj
        });
    }
}