using Shameless.Api.Auth;
using Shameless.Api.Contracts.Matches;
using Shameless.Api.Models.Requests.Game;
using Shameless.Api.Models.Responses.Game;
using Shameless.Application.Entities;
using Shameless.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Shameless.Api.Controllers;

[ApiController]
[Route("games")]
public sealed class GamesController : ControllerBase
{
    private readonly CustomGameService _gameService;

    public GamesController(CustomGameService gameService)
    {
        _gameService = gameService;
    }

    [HttpPost]
    public async Task<IActionResult> CreateGame([FromBody] CreateGameRequest request)
    {
        var (game, seriesId) = await _gameService.RecordCustomGame(
            blueTeamPlayerIds: request.BlueTeam,
            redTeamPlayerIds: request.RedTeam,
            seriesId: request.SeriesId,
            boFormat: request.BoFormat
        );

        return Ok(new CreateGameResponse(
            GameId: game.Id,
            PlayedAt: game.PlayedAt,
            SeriesId: seriesId,
            Message: "Game enregistrée !"
        ));
    }

    [HttpPut("{id}/result")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<SetGameResultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetGameResult(int id, [FromBody] SetGameResultRequest request)
    {
        if (!Equipes.IsValid(request.Winner))
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                detail: "Le vainqueur d'une game est une Équipe : « A » ou « B ».");

        var game = await _gameService.SetGameResultAsync(id, request.Winner);
        if (game == null) return NotFound();

        return Ok(new SetGameResultResponse(
            GameId: game.Id,
            Winner: game.WinningTeam,
            TeamASide: game.TeamASide,
            TeamBSide: game.TeamBSide,
            Status: game.Status
        ));
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
            Participants: g.Participants.Select(p => new PendingGameParticipant(  // ✅
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