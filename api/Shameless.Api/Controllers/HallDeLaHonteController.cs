using Microsoft.AspNetCore.Mvc;
using Shameless.Application.Services;
using Shameless.Application.Votes;

namespace Shameless.Api.Controllers;

/// <summary>Le Hall de la Honte : le cumul historique des titres de DOFD, sans remise à zéro mensuelle.</summary>
[ApiController]
[Route("hall-de-la-honte")]
public sealed class HallDeLaHonteController : ControllerBase
{
    private readonly HallDeLaHonteService _hall;

    public HallDeLaHonteController(HallDeLaHonteService hall) => _hall = hall;

    /// <summary>
    /// Les Joueurs ayant au moins un titre de DOFD (votes clos, égalités comprises), du plus au moins
    /// titré ; les ex æquo partagent le même rang (1, 1, 3).
    /// </summary>
    [HttpGet]
    [ProducesResponseType<HallDeLaHonte>(StatusCodes.Status200OK)]
    public async Task<ActionResult<HallDeLaHonte>> Get() => await _hall.GetAsync();
}
