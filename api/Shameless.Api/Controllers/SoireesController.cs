using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Responses.Soiree;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

[ApiController]
[Route("soirees")]
public sealed class SoireesController : ControllerBase
{
    private readonly SoireeService _soireeService;
    private readonly PickerService _pickerService;
    private readonly SeriesService _seriesService;

    public SoireesController(SoireeService soireeService, PickerService pickerService, SeriesService seriesService)
    {
        _soireeService = soireeService;
        _pickerService = pickerService;
        _seriesService = seriesService;
    }

    [HttpGet("open")]
    [ProducesResponseType<OpenSoireeResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<OpenSoireeResponse>> GetOpen()
    {
        var soiree = await _soireeService.GetOpenAsync();
        if (soiree == null) return new OpenSoireeResponse(null, await _soireeService.PickerInterrompuAsync());

        return new OpenSoireeResponse(SoireeResponse.From(soiree, await _pickerService.GetAsync(soiree.Id)));
    }

    /// <summary>Les Séries d'une Soirée et leur issue, de la plus ancienne à la plus récente.</summary>
    [HttpGet("{id:int}/series")]
    [ProducesResponseType<SoireeSeriesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<SoireeSeriesResponse>> GetSeries(int id)
    {
        if (await _soireeService.GetAsync(id) == null)
            return Problem(statusCode: StatusCodes.Status404NotFound, detail: "Cette Soirée n'existe pas.");

        return new SoireeSeriesResponse(await _seriesService.GetOfSoireeAsync(id));
    }

    [HttpPost]
    [ProducesResponseType<SoireeResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SoireeResponse>> Open()
    {
        var result = await _soireeService.OpenAsync();
        if (!result.IsOk) return this.Refusal(result);

        return Created("/soirees/open", SoireeResponse.From(result.Value!));
    }

    /// <summary>Ferme une Soirée ouverte ; refusé (409) tant qu'une de ses Séries est en cours.</summary>
    [HttpPost("{id:int}/close")]
    [ProducesResponseType<SoireeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SoireeResponse>> Close(int id)
    {
        var result = await _soireeService.CloseAsync(id);
        if (!result.IsOk) return this.Refusal(result);

        return SoireeResponse.From(result.Value!);
    }
}
