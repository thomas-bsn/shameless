using Shameless.Api.Contracts.Matches;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Responses.Game;
using Shameless.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Shameless.Api.Controllers;

/// <summary>
/// Les games. Une game se crée dans une Série (<c>POST /series/{id}/games</c>).
/// </summary>
[ApiController]
[Route("games")]
public sealed class GamesController : ControllerBase
{
    private readonly CustomGameService _gameService;
    private readonly SeriesService _seriesService;

    public GamesController(CustomGameService gameService, SeriesService seriesService)
    {
        _gameService = gameService;
        _seriesService = seriesService;
    }

    /// <summary>Saisit l'Équipe gagnante d'une game. Permis à tout utilisateur connecté.</summary>
    [HttpPut("{id}/result")]
    [ProducesResponseType<SetGameResultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SetGameResultResponse>> SetGameResult(int id, [FromBody] SetGameResultRequest request)
    {
        var result = await _seriesService.RecordResultAsync(id, request.Winner);
        if (!result.IsOk) return this.Refusal(result);

        var game = result.Value!;
        return new SetGameResultResponse(
            GameId: game.Id,
            Winner: game.WinningTeam,
            TeamASide: game.TeamASide,
            TeamBSide: game.TeamBSide,
            Status: game.Status
        );
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingGames()
    {
        var pendingGames = await _gameService.GetPendingGamesAsync();

        var response = pendingGames.Select(g => new PendingGameResponse(
            Id: g.Id,
            PlayedAt: g.PlayedAt,
            SeriesId: g.SeriesId,
            Status: g.Status,
            Participants: g.Participants.Select(p => new PendingGameParticipant(
                PlayerId: p.PlayerId,
                Team: p.Team,
                Equipe: g.EquipeOn(p.Team),
                Prenom: p.Player.Prenom
            )).ToList()
        )).ToList();

        return Ok(response);
    }

    [HttpGet("history/{year}/{month}")]
    public async Task<IActionResult> GetHistory(int year, int month)
    {
        var games = await _gameService.GetHistoryAsync(year, month);
        return Ok(new { games });
    }
}
