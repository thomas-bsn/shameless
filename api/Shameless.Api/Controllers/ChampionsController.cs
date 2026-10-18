using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Models.Responses.Champion;
using Shameless.Application.Interfaces;

namespace Shameless.Api.Controllers;

/// <summary>
/// Le catalogue des champions pour la Draft de champions. Données statiques publiques de Data Dragon :
/// lisible sans session, pour les capitaines et spectateurs qui arrivent par un lien (ADR 0010).
/// </summary>
[ApiController]
[Route("champions")]
[AllowAnonymous]
public sealed class ChampionsController : ControllerBase
{
    private readonly IChampionCatalog _catalog;

    public ChampionsController(IChampionCatalog catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Tous les champions, triés par nom.</summary>
    [HttpGet]
    [ProducesResponseType<List<ChampionResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<List<ChampionResponse>>> GetAll(CancellationToken cancellationToken)
    {
        var champions = await _catalog.GetAllAsync(cancellationToken);
        return champions.Select(c => new ChampionResponse(c.Id, c.Key, c.Name, c.ImageUrl)).ToList();
    }
}
