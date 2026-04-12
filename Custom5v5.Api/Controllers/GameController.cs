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
        var (game, seriesId) = await _gameService.RecordCustomGame(
            blueTeamPlayerIds: request.BlueTeam,
            redTeamPlayerIds: request.RedTeam,
            playedAt: DateTime.UtcNow,
            seriesId: request.SeriesId,
            boFormat: request.BoFormat
        );

        return Ok(new
        {
            message = "Game enregistrée !",
            gameId = game.Id,
            playedAt = game.PlayedAt,
            seriesId
        });
    }
    
    [HttpPut("series/{seriesId}/abandon")]
    public async Task<IActionResult> AbandonSeries(int seriesId)
    {
        var ok = await _gameService.AbandonSeriesAsync(seriesId);
        if (!ok) return NotFound();
        return Ok(new { success = true });
    }
    
    [HttpGet("series/active")]
    public async Task<IActionResult> GetActiveSeries()
    {
        var series = await _gameService.GetActiveSeriesAsync();
        return Ok(new { series });
    }
    
    [HttpGet("series/{seriesId}")]
    public async Task<IActionResult> GetSeriesStatus(int seriesId)
    {
        var status = await _gameService.GetSeriesStatusAsync(seriesId);
        if (status == null) return NotFound();
        return Ok(status);
    }
    
    [HttpGet("history/{year}/{month}")]
    public async Task<IActionResult> GetHistory(int year, int month)
    {
        var games = await _gameService.GetHistoryAsync(year, month);
        return Ok(new { games });
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
            seriesId = g.SeriesId,
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