using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shameless.Application.Interfaces;

namespace Shameless.Api.Controllers;

/// <summary>
/// Le catalogue des objets, sorts d'invocateur et runes : noms en français et icônes de Data Dragon, pour
/// afficher les identifiants du client que portent les lectures d'une game. Données statiques publiques,
/// lisibles sans session comme le catalogue des champions.
/// </summary>
[ApiController]
[Route("catalogue")]
[AllowAnonymous]
public sealed class CatalogueController : ControllerBase
{
    private readonly ICatalogueDeJeu _catalogue;

    public CatalogueController(ICatalogueDeJeu catalogue)
    {
        _catalogue = catalogue;
    }

    /// <summary>Les objets, sorts d'invocateur et arbres de runes de la version courante du jeu.</summary>
    [HttpGet]
    [ProducesResponseType<CatalogueDeJeu>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<CatalogueDeJeu>> Get(CancellationToken cancellationToken) =>
        await _catalogue.GetAsync(cancellationToken);
}
