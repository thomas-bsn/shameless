using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Auth;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Responses.Companion;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>
/// Le code Companion d'une Série. L'organisateur connecté le lit et le renouvelle ; le Companion le
/// résout sans connexion en sa Série.
/// </summary>
[ApiController]
public sealed class CompanionController : ControllerBase
{
    private readonly CodeCompanionService _codes;

    public CompanionController(CodeCompanionService codes) => _codes = codes;

    /// <summary>Le code Companion de la Série, à saisir dans le Companion.</summary>
    [HttpGet("series/{id:int}/code-companion")]
    [ProducesResponseType<CodeCompanionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<CodeCompanionResponse>> Get(int id)
    {
        var result = await _codes.GetAsync(id);
        if (!result.IsOk) return this.Refusal(result);

        return CodeCompanionResponse.From(result.Value!);
    }

    /// <summary>Renouvelle le code Companion d'une Série en cours : l'ancien est refusé.</summary>
    [HttpPost("series/{id:int}/code-companion/renouveler")]
    [ProducesResponseType<CodeCompanionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CodeCompanionResponse>> Renouveler(int id)
    {
        var result = await _codes.RenouvelerAsync(id);
        if (!result.IsOk) return this.Refusal(result);

        return CodeCompanionResponse.From(result.Value!);
    }

    /// <summary>
    /// Sans connexion : la Série d'un code Companion, pour que le Companion confirme à quoi il est
    /// associé. Code inconnu, renouvelé ou de Série terminée : 404.
    /// </summary>
    [HttpGet("companion/{code}")]
    [AccesParCodeCompanion]
    [ProducesResponseType<SerieDuCompanionResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public ActionResult<SerieDuCompanionResponse> Resoudre(string code) =>
        SerieDuCompanionResponse.From(this.SerieDuCodeCompanion());
}
