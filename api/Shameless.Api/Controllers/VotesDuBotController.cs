using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Auth;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Requests.Votes;
using Shameless.Application.Services;
using Shameless.Application.Votes;

namespace Shameless.Api.Controllers;

/// <summary>
/// Les votes MVP et DOFD déposés par le bot Discord (identité machine, ADR 0013) : présence des
/// Spectateurs en vocal, bulletins et résultats d'une Game.
/// </summary>
[ApiController]
[Route("bot/games/{id:int}")]
[AccesParBot]
[Tags("Bot")]
public sealed class VotesDuBotController : ControllerBase
{
    private readonly VotesService _votes;

    public VotesDuBotController(VotesService votes) => _votes = votes;

    /// <summary>
    /// Remplace les Spectateurs présents dans un salon vocal pendant la Game : ils pourront voter.
    /// Refusé (409) une fois les votes clos.
    /// </summary>
    [HttpPut("spectateurs")]
    [ProducesResponseType<SpectateursDeLaGame>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SpectateursDeLaGame>> SignalerSpectateurs(int id, [FromBody] SignalerSpectateursRequest request)
    {
        var result = await _votes.SignalerSpectateursAsync(id, request.DiscordIds);
        return result.IsOk ? result.Value! : this.Refusal(result);
    }

    /// <summary>
    /// Dépose ou remplace le bulletin d'un votant pendant les 10 minutes qui suivent la fin de la Game :
    /// au plus un Joueur par titre (null pour s'abstenir), jamais soi-même. 409 avant la fin de la Game
    /// ou une fois les votes clos, 400 pour un votant non éligible ou un choix invalide.
    /// </summary>
    [HttpPut("votes")]
    [ProducesResponseType<BulletinDuVotant>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<BulletinDuVotant>> Voter(int id, [FromBody] VoterRequest request)
    {
        var result = await _votes.VoterAsync(id, request.VotantDiscordId, request.MvpPlayerId, request.DofdPlayerId);
        return result.IsOk ? result.Value! : this.Refusal(result);
    }

    /// <summary>Les résultats des votes de la Game, à annoncer à leur clôture.</summary>
    [HttpGet("votes")]
    [ProducesResponseType<ResultatsDesVotes>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<ResultatsDesVotes>> GetResultats(int id) =>
        await _votes.GetResultatsAsync(id) is { } resultats
            ? resultats
            : Problem(statusCode: StatusCodes.Status404NotFound, detail: "Cette game n'existe pas.");
}
