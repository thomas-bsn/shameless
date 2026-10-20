using Microsoft.AspNetCore.Mvc;
using Shameless.Application.Services;
using Shameless.Application.Votes;

namespace Shameless.Api.Controllers;

/// <summary>Les résultats des votes DOFD d'une Game, affichés dans son récap.</summary>
[ApiController]
[Route("games/{id:int}/votes")]
public sealed class VotesController : ControllerBase
{
    private readonly VotesService _votes;

    public VotesController(VotesService votes) => _votes = votes;

    /// <summary>
    /// Les résultats des votes de la Game : décompte des voix, et gagnants (tous en cas d'égalité) une
    /// fois les votes clos, 10 minutes après la fin de la Game.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<ResultatsDesVotes>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ResultatsDesVotes>> GetResultats(int id) =>
        await _votes.GetResultatsAsync(id) is { } resultats
            ? resultats
            : Problem(statusCode: StatusCodes.Status404NotFound, detail: "Cette game n'existe pas.");
}
