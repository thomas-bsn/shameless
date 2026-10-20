using Shameless.Api.Auth;
using Shameless.Api.Models.Requests.Player;
using Shameless.Api.Models.Responses.Player;
using Shameless.Application.DTOs.Players;
using Shameless.Application.Interfaces;
using Shameless.Application.Joueurs;
using Shameless.Application.Picker;
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
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetPlayer(int playerId)
    {
        var player = await _playerService.GetPlayerAsync(playerId);
        return player == null ? JoueurIntrouvable() : Ok(player);
    }

    /// <summary>
    /// La fiche du Joueur : stats de customs sur ses games terminées, champions et rôles joués, notes de
    /// performance et historique, compte Discord lié, cost courant et rangs actuels, « Joué avec », résumé
    /// des 30 dernières games et liste des games. Les paramètres regroupent (<c>regroupement</c> : plat,
    /// soirée, jour) et filtrent (rôle, série, champion, résultat) la liste seulement.
    /// </summary>
    [HttpGet("{playerId:int}/fiche")]
    [ProducesResponseType<FicheJoueur>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetFiche(
        int playerId,
        [FromServices] FicheJoueurService fiches,
        [FromQuery] Regroupement regroupement = Regroupement.Plat,
        [FromQuery] Role? role = null,
        [FromQuery] int? serieId = null,
        [FromQuery] string? champion = null,
        [FromQuery] ResultatFiltre? resultat = null)
    {
        var fiche = await fiches.GetAsync(playerId, new FiltreDeGames(regroupement, role, serieId, champion, resultat));
        return fiche == null ? JoueurIntrouvable() : Ok(fiche);
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
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> LinkPlayer([FromBody] LinkPlayerRequest request)
    {
        var result = await _playerService.LinkPlayerAsync(User.DiscordId(), request.PlayerId, User.Username(), User.AvatarUrl());

        return result switch
        {
            LinkResult.PlayerNotFound => JoueurIntrouvable(),
            LinkResult.AlreadyLinkedToAnotherPlayer => Problem(
                statusCode: StatusCodes.Status409Conflict,
                detail: "Ton compte Discord est déjà lié à un autre joueur. Délie-le d'abord."),
            _ => Ok(new PlayerLinkResponse(Success: true))
        };
    }

    /// <summary>
    /// Délie l'Utilisateur connecté de son Joueur : seule l'association disparaît. Le Joueur et ses stats,
    /// l'Utilisateur et son Compte Discord restent, et on peut se relier ensuite. Toujours pour soi-même.
    /// </summary>
    [HttpDelete("link")]
    [ProducesResponseType<PlayerLinkResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> UnlinkPlayer()
    {
        var unlinked = await _playerService.UnlinkPlayerAsync(User.DiscordId());
        return unlinked ? Ok(new PlayerLinkResponse(Success: true)) : NoLinkedPlayer();
    }

    /// <summary>Le cost du Joueur pour le mois en cours, avec le détail de son calcul (ADR 0012).</summary>
    [HttpGet("{playerId:int}/cost")]
    [ProducesResponseType<PlayerCostResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetPlayerCost(int playerId)
    {
        var cost = await _costService.CalculatePlayerCostPreview(playerId);
        return cost == null ? JoueurIntrouvable() : Ok(PlayerCostResponse.From(cost));
    }

    /// <summary>Le cost de chaque mois du Joueur conservé dans l'historique, du plus ancien au plus récent.</summary>
    [HttpGet("{playerId:int}/cost/historique")]
    [ProducesResponseType<PlayerCostHistoryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetPlayerCostHistory(int playerId)
    {
        var historique = await _costService.HistoriqueAsync(playerId);
        return historique == null
            ? JoueurIntrouvable()
            : Ok(new PlayerCostHistoryResponse(playerId, historique.Select(CostMensuelHistorique.From).ToList()));
    }

    [HttpPut("{playerId:int}/cost")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> SetPlayerCost(int playerId, [FromBody] SetCostRequest request)
    {
        var cost = await _costService.FixerCostAsync(playerId, request.Cost);
        return cost == null ? JoueurIntrouvable() : Ok(new { playerId, cost });
    }

    [HttpPost("{playerId:int}/peak")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> SetPlayerPeak(int playerId, [FromBody] UpdatePeakRequest request)
    {
        var result = await _playerService.UpdatePeakForPlayerAsync(playerId, request.PeakTier, request.PeakDivision, request.PeakSeason, request.PeakLp);
        return result switch
        {
            PeakResult.Updated => Ok(new { ok = true }),
            PeakResult.InvalidSeason => SaisonInvalide(),
            _ => JoueurIntrouvable(),
        };
    }

    [HttpDelete("{playerId:int}/peak")]
    [Authorize(Policy = Policies.Admin)]
    public async Task<IActionResult> DeletePlayerPeak(int playerId)
    {
        var result = await _playerService.UpdatePeakForPlayerAsync(playerId, null, null, null, 0);
        return result == PeakResult.Updated ? Ok(new { ok = true }) : JoueurIntrouvable();
    }

    /// <summary>Les saisons qu'on peut choisir pour un peak, de S24 à la saison courante.</summary>
    [HttpGet("peak/saisons")]
    [ProducesResponseType<PeakSaisonsResponse>(StatusCodes.Status200OK)]
    public IActionResult GetPeakSaisons([FromServices] TimeProvider clock)
    {
        var annee = clock.GetUtcNow().Year;
        return Ok(new PeakSaisonsResponse(SaisonsDePeak.Jusqua(annee), SaisonsDePeak.Courante(annee)));
    }

    [HttpPost("peak")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    public async Task<IActionResult> UpdatePeak([FromBody] UpdatePeakRequest request)
    {
        var result = await _playerService.UpdatePeakAsync(User.DiscordId(), request.PeakTier, request.PeakDivision, request.PeakSeason, request.PeakLp);
        return result switch
        {
            PeakResult.Updated => Ok(new { ok = true }),
            PeakResult.InvalidSeason => SaisonInvalide(),
            _ => NoLinkedPlayer(),
        };
    }

    [HttpDelete("peak")]
    public async Task<IActionResult> DeletePeak()
    {
        var result = await _playerService.UpdatePeakAsync(User.DiscordId(), null, null, null, 0);
        return result == PeakResult.Updated ? Ok(new { ok = true }) : NoLinkedPlayer();
    }

    private ObjectResult SaisonInvalide() =>
        Problem(statusCode: StatusCodes.Status400BadRequest, detail: "La saison du peak doit aller de S24 à la saison courante.");

    /// <summary>La réponse unique à un Joueur inconnu.</summary>
    private ObjectResult JoueurIntrouvable() =>
        Problem(statusCode: StatusCodes.Status404NotFound, detail: "Ce joueur n'existe pas.");

    private ObjectResult NoLinkedPlayer() =>
        Problem(statusCode: StatusCodes.Status404NotFound, detail: "Aucun joueur n'est lié à ton compte Discord.");
}