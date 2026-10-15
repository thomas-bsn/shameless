using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Auth;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Requests.ChampionDraft;
using Shameless.Api.Models.Responses.ChampionDraft;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>
/// La Draft de champions d'une game. L'organisateur connecté l'ouvre et la lit ; les capitaines la
/// jouent et la lisent par leur Lien capitaine, le spectateur la lit par le Lien spectateur (ADR 0010).
/// </summary>
[ApiController]
public sealed class DraftChampionsController : ControllerBase
{
    private readonly DraftChampionsService _drafts;

    public DraftChampionsController(DraftChampionsService drafts) => _drafts = drafts;

    /// <summary>
    /// Ouvre la Draft de champions de la game en attente de la Série : Side connu et deux capitaines
    /// désignés. Une seule Draft de champions par game.
    /// </summary>
    [HttpPost("series/{id:int}/draft-champions")]
    [ProducesResponseType<DraftChampionsResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DraftChampionsResponse>> Ouvrir(int id)
    {
        var result = await _drafts.OuvrirAsync(id);
        if (!result.IsOk) return this.Refusal(result);

        var draft = DraftChampionsResponse.From(result.Value!);
        return Created($"/games/{draft.GameId}/draft-champions", draft);
    }

    /// <summary>La Draft de champions d'une game, en cours ou terminée.</summary>
    [HttpGet("games/{gameId:int}/draft-champions")]
    [ProducesResponseType<DraftChampionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<DraftChampionsResponse>> Get(int gameId)
    {
        var result = await _drafts.GetByGameAsync(gameId);
        if (!result.IsOk) return this.Refusal(result);

        return DraftChampionsResponse.From(result.Value!);
    }

    /// <summary>Sans connexion, par un Lien capitaine ou le Lien spectateur : la dernière Draft de champions de la Série.</summary>
    [HttpGet("liens/{lien}/draft-champions")]
    [AccesParLien]
    [ProducesResponseType<DraftChampionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<DraftChampionsResponse>> GetParLien(string lien)
    {
        var result = await _drafts.GetParLienAsync(this.AccesParLien());
        if (!result.IsOk) return this.Refusal(result);

        return DraftChampionsResponse.From(result.Value!);
    }

    /// <summary>
    /// Sans connexion, par un Lien capitaine : joue le tour attendu (ban ou pick) pour l'Équipe du lien.
    /// Tour déjà joué, hors tour ou de l'autre Équipe, champion déjà banni ou pick : 409 ; champion
    /// inconnu : 400 ; Lien spectateur : 403.
    /// </summary>
    [HttpPost("liens/{lien}/draft-champions/actions")]
    [AccesParLien(CapitaineRequis = true)]
    [ProducesResponseType<DraftChampionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<DraftChampionsResponse>> Jouer(string lien, [FromBody] JouerDraftChampionsRequest request)
    {
        try
        {
            var result = await _drafts.JouerParLienAsync(this.AccesParLien(), request.Tour, request.ChampionId);
            if (!result.IsOk) return this.Refusal(result);

            return DraftChampionsResponse.From(result.Value!);
        }
        catch (Exception e) when (e is HttpRequestException or System.Text.Json.JsonException)
        {
            return Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
                detail: "Le catalogue des champions est indisponible pour le moment (Data Dragon).");
        }
    }
}
