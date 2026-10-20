using Microsoft.AspNetCore.Mvc;
using Shameless.Application.Pronostics;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>Les pronostics des Spectateurs sur une Game, affichés dans son récap.</summary>
[ApiController]
[Route("games/{id:int}/pronostics")]
public sealed class PronosticsController : ControllerBase
{
    private readonly PronosticsService _pronostics;

    public PronosticsController(PronosticsService pronostics) => _pronostics = pronostics;

    /// <summary>
    /// Les pronostics de la Game, chaque choix marqué juste ou faux dès que son résultat est connu
    /// (vainqueur à la fin de la Game, DOFD à la clôture des votes). Ni points ni classement.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PronosticsDeLaGame>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<PronosticsDeLaGame>> GetDeLaGame(int id) =>
        await _pronostics.GetDeLaGameAsync(id) is { } pronostics
            ? pronostics
            : Problem(statusCode: StatusCodes.Status404NotFound, detail: "Cette game n'existe pas.");
}
