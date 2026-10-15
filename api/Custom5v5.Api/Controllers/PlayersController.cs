using System.Security.Claims;
using Custom5v5.Api.Models.Requests.Player;
using Custom5v5.Api.Models.Responses.Player;
using Custom5v5.Application.DTOs.Players;
using Custom5v5.Application.Interfaces;
using Custom5v5.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Custom5v5.Api.Controllers;

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
    [Authorize]
    public async Task<IActionResult> GetMyPlayer()
    {
        var discordUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(discordUserId)) return Unauthorized();

        var player = await _playerService.GetPlayerByDiscordIdAsync(discordUserId);
        if (player == null) return NotFound(new { error = "no_linked_player" });

        return Ok(player);
    }

    [HttpPost("link")]
    [Authorize]
    public async Task<IActionResult> LinkPlayer([FromBody] LinkPlayerRequest request)
    {
        var discordUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var username = User.FindFirstValue(ClaimTypes.Name);
        var avatarUrl = User.FindFirstValue("avatar_url");

        if (string.IsNullOrWhiteSpace(discordUserId)) return Unauthorized();

        await _playerService.LinkPlayerAsync(discordUserId, request.PlayerId, username, avatarUrl);
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

    [HttpPost("peak")]
    [Authorize]
    public async Task<IActionResult> UpdatePeak([FromBody] UpdatePeakRequest request)
    {
        var discordUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(discordUserId)) return Unauthorized();

        await _playerService.UpdatePeakAsync(discordUserId, request.PeakTier, request.PeakDivision, request.PeakSeason, request.PeakLp);
        return Ok(new { ok = true });
    }

    [HttpDelete("peak")]
    [Authorize]
    public async Task<IActionResult> DeletePeak()
    {
        var discordUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(discordUserId)) return Unauthorized();

        await _playerService.UpdatePeakAsync(discordUserId, null, null, null, 0);
        return Ok(new { ok = true });
    }
}