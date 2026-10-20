using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Auth;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Requests.Pronostics;
using Shameless.Application.Pronostics;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>
/// Les pronostics des Spectateurs déposés par le bot Discord (identité machine, ADR 0013) : sur la
/// prochaine Game d'une Série, verrouillés à son lancement, puis évalués après la Game.
/// </summary>
[ApiController]
[Route("bot")]
[AccesParBot]
[Tags("Bot")]
public sealed class PronosticsDuBotController : ControllerBase
{
    private readonly PronosticsService _pronostics;

    public PronosticsDuBotController(PronosticsService pronostics) => _pronostics = pronostics;

    /// <summary>
    /// La prochaine Game de la Série ouverte aux pronostics (null s'ils sont verrouillés) et la
    /// composition courante des Équipes, avec l'ID Discord des Joueurs liés (exclus des pronostics).
    /// </summary>
    [HttpGet("series/{id:int}/pronostics")]
    [ProducesResponseType<PronosticsDeLaSerie>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PronosticsDeLaSerie>> GetOuverts(int id) =>
        await _pronostics.GetOuvertsAsync(id) is { } ouverts
            ? ouverts
            : Problem(statusCode: StatusCodes.Status404NotFound, detail: "Cette Série n'existe pas.");

    /// <summary>
    /// Dépose ou remplace le pronostic d'un Spectateur sur la prochaine Game de la Série : Équipe
    /// gagnante et DOFD, chacun facultatif (deux null retirent le pronostic). 409 une fois la Game
    /// lancée ou la Série terminée ; 400 pour un Joueur de la Série, une autre Game ou un choix invalide.
    /// </summary>
    [HttpPut("series/{id:int}/pronostics")]
    [ProducesResponseType<PronosticDuSpectateur>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<PronosticDuSpectateur>> Pronostiquer(int id, [FromBody] PronostiquerRequest request)
    {
        var result = await _pronostics.PronostiquerAsync(
            id, request.PronostiqueurDiscordId, request.PronostiqueurNom, request.GameNumber,
            request.EquipeGagnante, request.DofdPlayerId);
        return result.IsOk ? result.Value! : this.Refusal(result);
    }

    /// <summary>Les pronostics évalués d'une Game, à annoncer après la Game et la clôture des votes.</summary>
    [HttpGet("games/{id:int}/pronostics")]
    [ProducesResponseType<PronosticsDeLaGame>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PronosticsDeLaGame>> GetDeLaGame(int id) =>
        await _pronostics.GetDeLaGameAsync(id) is { } pronostics
            ? pronostics
            : Problem(statusCode: StatusCodes.Status404NotFound, detail: "Cette game n'existe pas.");
}
