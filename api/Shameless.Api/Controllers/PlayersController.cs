using Shameless.Api.Auth;
using Shameless.Api.Models.Requests.Player;
using Shameless.Api.Models.Responses.Player;
using Shameless.Application.DTOs.Players;
using Shameless.Application.Interfaces;
using Shameless.Application.Joueurs;
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
    [ProducesResponseType<List<PlayerDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlayers()
    {
        var players = await _playerService.GetPlayersAsync();
        return Ok(players);
    }

    [HttpGet("{playerId:int}")]
    [ProducesResponseType<PlayerDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPlayer(int playerId)
    {
        var player = await _playerService.GetPlayerAsync(playerId);
        return player == null ? NotFound() : Ok(player);
    }

    /// <summary>
    /// La fiche du Joueur : stats de customs sur ses games terminées, champions et rôles joués, notes de
    /// performance et historique, compte Discord lié, cost courant et rangs actuels.
    /// </summary>
    [HttpGet("{playerId:int}/fiche")]
    [ProducesResponseType<FicheJoueur>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetFiche(int playerId, [FromServices] FicheJoueurService fiches)
    {
        var fiche = await fiches.GetAsync(playerId);
        return fiche == null
            ? Problem(statusCode: StatusCodes.Status404NotFound, detail: "Joueur introuvable.")
            : Ok(fiche);
    }

    /// <summary>Le compte Riot tel qu'il serait enregistré, sans créer de Joueur.</summary>
    [HttpGet("riot-preview")]
    [ProducesResponseType<RiotAccountPreview>(StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewRiotAccount([FromQuery] string riotId)
    {
        var preview = await _playerService.PreviewRiotAccountAsync(riotId);
        return preview == null
            ? Problem(statusCode: StatusCodes.Status404NotFound, detail: "Compte Riot introuvable. Vérifie le RiotId (Pseudo#TAG).")
            : Ok(preview);
    }

    [HttpPost]
    [ProducesResponseType<PlayerResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> CreatePlayer([FromBody] CreatePlayerRequest request)
    {
        var player = await _playerService.CreatePlayerAsync(request.Prenom, request.RiotId);
        return Ok(player);
    }

    [HttpGet("me")]
    [ProducesResponseType<PlayerDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMyPlayer()
    {
        var player = await _playerService.GetPlayerByDiscordIdAsync(User.DiscordId());
        if (player == null) return NoLinkedPlayer();

        return Ok(player);
    }

    [HttpPost("link")]
    [ProducesResponseType<PlayerLinkResponse>(StatusCodes.Status200OK)]
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
    [ProducesResponseType<PlayerCostResponse>(StatusCodes.Status200OK)]
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