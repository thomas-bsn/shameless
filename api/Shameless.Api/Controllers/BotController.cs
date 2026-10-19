using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Auth;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Requests.Series;
using Shameless.Application.Bot;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>
/// Les routes du bot Discord de l'Instance, ouvertes par sa clé et par elle seule (identité machine
/// bot → API, ADR 0013). Seules les opérations dont le bot a besoin y figurent.
/// </summary>
[ApiController]
[Route("bot")]
[AccesParBot]
[Tags("Bot")]
public sealed class BotController : ControllerBase
{
    private readonly CustomGameService _games;
    private readonly LiensAccesService _liens;

    public BotController(CustomGameService games, LiensAccesService liens)
    {
        _games = games;
        _liens = liens;
    }

    /// <summary>
    /// Le DM du Lien capitaine de l'Équipe ("A" | "B") n'est pas parti (<c>captain_link</c>) : compte Discord
    /// non lié, DM fermé… L'organisateur le voit sur l'Écran de préparation et copie le lien à la main.
    /// </summary>
    [HttpPost("series/{id:int}/liens-capitaine/{equipe}/dm-echec")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> EchecDm(int id, string equipe, EchecDmRequest request)
    {
        var result = await _liens.SignalerEchecDmAsync(id, equipe, request.Raison);
        return result.IsOk ? NoContent() : this.Refusal(result);
    }

    /// <summary>
    /// Une game, en attente ou terminée, avec ses dix joueurs et l'ID Discord de leur compte lié :
    /// le contexte des votes et pronostics proposés par le bot.
    /// </summary>
    [HttpGet("games/{id:int}")]
    [ProducesResponseType<GameDuBot>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<GameDuBot>> GetGame(int id) =>
        await _games.GetPourLeBotAsync(id) is { } game
            ? game
            : Problem(statusCode: StatusCodes.Status404NotFound, detail: "Cette game n'existe pas.");
}
