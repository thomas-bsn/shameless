using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Auth;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Requests.Companion;
using Shameless.Api.Models.Responses.Companion;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>
/// Le code Companion d'une Série. L'organisateur connecté le lit et le renouvelle ; le Companion le
/// résout sans connexion en sa Série et y envoie les rapports de fin de game.
/// </summary>
[ApiController]
public sealed class CompanionController : ControllerBase
{
    private readonly CodeCompanionService _codes;
    private readonly RapportsCompanionService _rapports;
    private readonly SeriesService _series;

    public CompanionController(CodeCompanionService codes, RapportsCompanionService rapports, SeriesService series)
    {
        _codes = codes;
        _rapports = rapports;
        _series = series;
    }

    /// <summary>
    /// Sans connexion : le signal « partie lancée » du Companion, le Début de la Game (ADR 0017). Il
    /// renseigne le début de la game en attente de la Série du code, clôt ses pronostics et émet
    /// <c>game_started</c>. Idempotent : un second signal répond le même début sans rien ré-émettre.
    /// Aucune game en attente : 409.
    /// </summary>
    [HttpPost("companion/{code}/debut")]
    [AccesParCodeCompanion]
    [ProducesResponseType<DebutDeGameResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DebutDeGameResponse>> SignalerDebut(string code)
    {
        var result = await _series.SignalerDebutAsync(this.SerieDuCodeCompanion().Id);
        if (!result.IsOk) return this.Refusal(result);

        return DebutDeGameResponse.From(result.Value!);
    }

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

    /// <summary>
    /// Sans connexion : le rapport de fin de game du Companion. Si les joueurs détectés sont les
    /// participants de la game en attente de la Série du code (Remplacements compris), chacun sur son
    /// Side, la game est terminée avec le vainqueur du rapport, comme une saisie manuelle, et garde ses
    /// données Riot et les champions joués (statut Accepte). Sinon le rapport est en revue (statut
    /// EnRevue) : la game reste en attente de la décision de l'organisateur. Rapport mal formé : 400 ;
    /// aucune game en attente, ou un autre rapport de la game déjà en revue : 409. Série terminée : un
    /// nouveau rapport est refusé (404, comme le code), mais le renvoi d'un rapport déjà reçu désigne
    /// encore sa game, sans rien modifier. Un rapport déjà reçu (même identifiant Riot) n'est jamais
    /// traité deux fois : son renvoi répond son statut (Accepte, EnRevue ou Rejete) sans rien créer.
    /// </summary>
    [HttpPost("companion/{code}/rapports")]
    [AccesParCodeCompanion(SerieTermineeAdmise = true)]
    [ProducesResponseType<RapportTraiteResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<RapportTraiteResponse>> Rapporter(string code, RapportDeGameRequest request)
    {
        var result = await _rapports.RecevoirAsync(this.SerieDuCodeCompanion().Id, request.ToRapport());
        if (!result.IsOk) return this.Refusal(result);

        return RapportTraiteResponse.From(result.Value!);
    }
}
