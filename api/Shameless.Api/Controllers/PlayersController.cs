using System.Security.Claims;
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
        var discordUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(discordUserId)) return Unauthorized();

        var player = await _playerService.GetPlayerByDiscordIdAsync(discordUserId);
        if (player == null) return NotFound(new { error = "no_linked_player" });

        return Ok(player);
    }

    [HttpPost("link")]
    public async Task<IActionResult> LinkPlayer([FromBody] LinkPlayerRequest request, [FromServices] IAuthorizationService authorization)
    {
        var discordUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var username = User.FindFirstValue(ClaimTypes.Name);
        var avatarUrl = User.FindFirstValue("avatar_url");

        if (string.IsNullOrWhiteSpace(discordUserId)) return Unauthorized();

        var isAdmin = (await authorization.AuthorizeAsync(User, Policies.Admin)).Succeeded;
        var result = await _playerService.LinkPlayerAsync(discordUserId, request.PlayerId, username, avatarUrl, canTakeOver: isAdmin);
        if (result == LinkResult.AlreadyLinkedToAnotherUser)
            return Conflict(new { error = "player_already_linked" });

        return Ok(new PlayerLinkResponse(Success: true));
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
        var discordUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(discordUserId)) return Unauthorized();

        await _playerService.UpdatePeakAsync(discordUserId, request.PeakTier, request.PeakDivision, request.PeakSeason, request.PeakLp);
        return Ok(new { ok = true });
    }

    [HttpDelete("peak")]
    public async Task<IActionResult> DeletePeak()
    {
        var discordUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(discordUserId)) return Unauthorized();

        await _playerService.UpdatePeakAsync(discordUserId, null, null, null, 0);
        return Ok(new { ok = true });
    }
}