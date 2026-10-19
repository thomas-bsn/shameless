using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Auth;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Responses.Series;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>
/// Les liens d'accès d'une Série (ADR 0010). L'organisateur connecté les lit, les renouvelle ; un lien se résout sans connexion en l'accès qu'il donne.
/// </summary>
[ApiController]
public sealed class LiensController : ControllerBase
{
    private readonly LiensAccesService _liens;

    public LiensController(LiensAccesService liens) => _liens = liens;

    /// <summary>Les deux Liens capitaine de la Série et son Lien spectateur.</summary>
    [HttpGet("series/{id:int}/liens")]
    [ProducesResponseType<LiensSerieResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<LiensSerieResponse>> Get(int id)
    {
        var result = await _liens.GetAsync(id);
        if (!result.IsOk) return this.Refusal(result);

        return LiensSerieResponse.From(result.Value!);
    }

    /// <summary>Renouvelle le Lien capitaine de l'Équipe ("A" | "B") : l'ancien est refusé.</summary>
    [HttpPost("series/{id:int}/liens/{equipe}/renouveler")]
    [ProducesResponseType<LiensSerieResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<LiensSerieResponse>> Renouveler(int id, string equipe)
    {
        var result = await _liens.RenouvelerLienCapitaineAsync(id, equipe);
        if (!result.IsOk) return this.Refusal(result);

        return LiensSerieResponse.From(result.Value!);
    }

    /// <summary>
    /// Sans connexion : ce que donne un lien (sa Série, sa portée, son Équipe). Lien inconnu ou renouvelé : 404.
    /// </summary>
    [HttpGet("liens/{lien}")]
    [AccesParLien]
    [ProducesResponseType<AccesParLienResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<AccesParLienResponse>> Resoudre(string lien)
    {
        // Ouvrir un Lien capitaine, c'est rejoindre la Série (ADR 0017).
        await _liens.MarquerRejointAsync(lien);
        return AccesParLienResponse.From(this.AccesParLien());
    }
}
