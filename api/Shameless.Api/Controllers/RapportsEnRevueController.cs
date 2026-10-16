using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Responses.Companion;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>
/// La décision de l'organisateur connecté sur un rapport du Companion en revue : il l'accepte ou le
/// rejette. Le code Companion n'y donne pas accès.
/// </summary>
[ApiController]
public sealed class RapportsEnRevueController : ControllerBase
{
    private readonly RapportsCompanionService _rapports;

    public RapportsEnRevueController(RapportsCompanionService rapports)
    {
        _rapports = rapports;
    }

    /// <summary>
    /// L'organisateur accepte un rapport en revue : la game est terminée avec ce rapport, comme pour un
    /// rapport conforme. Rapport inconnu (ou disparu avec sa game) : 404 ; déjà décidé : 409.
    /// </summary>
    [HttpPost("rapports/{id:int}/accepter")]
    [ProducesResponseType<RapportTraiteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RapportTraiteResponse>> Accepter(int id)
    {
        var result = await _rapports.AccepterAsync(id);
        if (!result.IsOk) return this.Refusal(result);

        return RapportTraiteResponse.From(result.Value!);
    }

    /// <summary>
    /// L'organisateur rejette un rapport en revue : il est écarté, la game attend un autre rapport ou
    /// une saisie manuelle. Rapport inconnu (ou disparu avec sa game) : 404 ; déjà décidé : 409.
    /// </summary>
    [HttpPost("rapports/{id:int}/rejeter")]
    [ProducesResponseType<RapportTraiteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RapportTraiteResponse>> Rejeter(int id)
    {
        var result = await _rapports.RejeterAsync(id);
        if (!result.IsOk) return this.Refusal(result);

        return RapportTraiteResponse.From(result.Value!);
    }
}
