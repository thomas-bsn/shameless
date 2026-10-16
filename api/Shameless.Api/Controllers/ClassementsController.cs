using Microsoft.AspNetCore.Mvc;
using Shameless.Application.Classements;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>Les classements de l'Instance : customs 5v5 et SoloQ, consultés séparément.</summary>
[ApiController]
[Route("classements")]
public sealed class ClassementsController : ControllerBase
{
    private readonly ClassementsService _classements;

    public ClassementsController(ClassementsService classements) => _classements = classements;

    /// <summary>Le classement des customs 5v5 : par winrate sur les games terminées, avec les games jouées.</summary>
    [HttpGet("5v5")]
    [ProducesResponseType<Classement5v5>(StatusCodes.Status200OK)]
    public async Task<ActionResult<Classement5v5>> Get5v5() => await _classements.Get5v5Async();

    /// <summary>Le classement SoloQ : les rangs actuels, une liste par file classée (SoloQ et Flex).</summary>
    [HttpGet("soloq")]
    [ProducesResponseType<ClassementSoloQ>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClassementSoloQ>> GetSoloQ() => await _classements.GetSoloQAsync();
}
