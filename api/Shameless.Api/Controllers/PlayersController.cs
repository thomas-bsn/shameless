using Shameless.Api.Auth;
using Shameless.Api.Models.Requests.Player;
using Shameless.Api.Models.Responses.Player;
using Shameless.Application.DTOs.Players;
using Shameless.Application.Interfaces;
using Shameless.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Shameless.Api.Controllers;

[ApiController]
[Route("players")]
public sealed class PlayersController : ControllerBase
{
    private readonly IPlayerService _playerService;
    private readonly CostCalculationService _costService;

    public PlayersController(IPlayerService playerService, CostCalculationService costService)
    {
        _playerService = playerService;
        _costService = costService;
    }

    [HttpGet]
    public async Task<IActionResult> GetPlayers()
    {
        var players = await _playerService.GetPlayersAsync();
        return Ok(players);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePlayer([FromBody] CreatePlayerRequest request)
    {
        var player = await _playerService.CreatePlayerAsync(request.Prenom, request.RiotId);
        return Ok(player);
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetMyPlayer()
    {
        var player = await _playerService.GetPlayerByDiscordIdAsync(User.DiscordId());
        if (player == null) return NoLinkedPlayer();

        return Ok(player);
    }

    [HttpPost("link")]
    public async Task<IActionResult> LinkPlayer([FromBody] LinkPlayerRequest request, [FromServices] IAuthorizationService authorization)
    {
        var isAdmin = await authorization.IsAdminAsync(User);
        var result = await _playerService.LinkPlayerAsync(User.DiscordId(), request.PlayerId, User.Username(), User.AvatarUrl(), canTakeOver: isAdmin);

        return result switch
        {
            LinkResult.PlayerNotFound => NotFound(),
            LinkResult.AlreadyLinkedToAnotherUser => Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: "Ce joueur est déjà lié à un autre compte Discord. Demande à un admin."),
            _ => Ok(new PlayerLinkResponse(Success: true))
        };
    }

    [HttpGet("{playerId}/cost")]
    public async Task<IActionResult> GetPlayerCost(int playerId)
    {
        var (name, cost, mmr, baseMMR, winrateAdj) = await _costService.CalculatePlayerCostPreview(playerId);

        return Ok(new PlayerCostResponse(
            PlayerId: playerId,
            Name: name,
            Cost: cost,
            Tier: MMRCalculationService.GetTierLabel(cost),
            Mmr: mmr,
            BaseMMR: baseMMR,
            WinrateAdjustment: winrateAdj
        ));
    }

    [HttpPut("{playerId}/cost")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> SetPlayerCost(int playerId, [FromBody] SetCostRequest request)
    {
        if (!await _playerService.SetCostAsync(playerId, request.Cost)) return NotFound();
        return Ok(new { playerId, cost = request.Cost });
    }

    [HttpPost("{playerId}/peak")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> SetPlayerPeak(int playerId, [FromBody] UpdatePeakRequest request)
    {
        var found = await _playerService.UpdatePeakForPlayerAsync(playerId, request.PeakTier, request.PeakDivision, request.PeakSeason, request.PeakLp);
        return found ? Ok(new { ok = true }) : NotFound();
    }

    [HttpDelete("{playerId}/peak")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> DeletePlayerPeak(int playerId)
    {
        var found = await _playerService.UpdatePeakForPlayerAsync(playerId, null, null, null, 0);
        return found ? Ok(new { ok = true }) : NotFound();
    }

    [HttpPost("peak")]
    public async Task<IActionResult> UpdatePeak([FromBody] UpdatePeakRequest request)
    {
        var found = await _playerService.UpdatePeakAsync(User.DiscordId(), request.PeakTier, request.PeakDivision, request.PeakSeason, request.PeakLp);
        return found ? Ok(new { ok = true }) : NoLinkedPlayer();
    }

    [HttpDelete("peak")]
    public async Task<IActionResult> DeletePeak()
    {
        var found = await _playerService.UpdatePeakAsync(User.DiscordId(), null, null, null, 0);
        return found ? Ok(new { ok = true }) : NoLinkedPlayer();
    }

    private ObjectResult NoLinkedPlayer() =>
        Problem(statusCode: StatusCodes.Status404NotFound, detail: "Aucun joueur n'est lié à ton compte Discord.");
}