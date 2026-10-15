using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Models.Responses.Soiree;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

[ApiController]
[Route("soirees")]
public sealed class SoireesController : ControllerBase
{
    private readonly SoireeService _soireeService;

    public SoireesController(SoireeService soireeService) => _soireeService = soireeService;

    [HttpGet("open")]
    [ProducesResponseType<OpenSoireeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OpenSoireeResponse>> GetOpen()
    {
        var soiree = await _soireeService.GetOpenAsync();
        return new OpenSoireeResponse(soiree == null ? null : SoireeResponse.From(soiree));
    }

    [HttpPost]
    [ProducesResponseType<SoireeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SoireeResponse>> Open()
    {
        var soiree = await _soireeService.OpenAsync();
        if (soiree == null)
            return Problem(statusCode: StatusCodes.Status409Conflict, detail: "Une Soirée est déjà ouverte.");

        return Created("/soirees/open", SoireeResponse.From(soiree));
    }

    [HttpPost("{id:int}/close")]
    [ProducesResponseType<SoireeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SoireeResponse>> Close(int id)
    {
        var (result, soiree) = await _soireeService.CloseAsync(id);
        return result switch
        {
            CloseSoireeResult.NotFound => Problem(statusCode: StatusCodes.Status404NotFound, detail: "Cette Soirée n'existe pas."),
            CloseSoireeResult.AlreadyClosed => Problem(statusCode: StatusCodes.Status409Conflict, detail: "Cette Soirée est déjà fermée."),
            _ => SoireeResponse.From(soiree!)
        };
    }
}
