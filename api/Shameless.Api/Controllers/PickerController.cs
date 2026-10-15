using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Requests.Picker;
using Shameless.Api.Models.Responses.Picker;
using Shameless.Api.Models.Responses.Series;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>Le Picker de la Soirée ouverte. Son état est restitué avec la Soirée ouverte.</summary>
[ApiController]
[Route("soirees/open/picker")]
public sealed class PickerController : ControllerBase
{
    private readonly PickerService _pickerService;

    public PickerController(PickerService pickerService) => _pickerService = pickerService;

    /// <summary>Coche les dix joueurs : ouvre la session de Picker, ou remplace sa sélection.</summary>
    [HttpPut("selection")]
    [ProducesResponseType<PickerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<PickerResponse>> Select([FromBody] SelectPlayersRequest request)
    {
        var result = await _pickerService.SelectAsync(request.PlayerIds ?? []);
        if (!result.IsOk) return this.Refusal(result);
        return PickerResponse.From(result.Value!);
    }

    /// <summary>Tire les Équipes par Roulette, Aléatoire ou Équilibrée. Tirer à nouveau relance.</summary>
    [HttpPost("roulette")]
    [ProducesResponseType<PickerResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<PickerResponse>> TirerRoulette([FromBody] TirerRouletteRequest request)
    {
        var result = await _pickerService.TirerRouletteAsync(request.Variante);
        if (!result.IsOk) return this.Refusal(result);
        return PickerResponse.From(result.Value!);
    }

    /// <summary>Valide le tirage : crée la Série entre les deux Équipes et clôt la session de Picker.</summary>
    [HttpPost("validate")]
    [ProducesResponseType<CreateSeriesResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CreateSeriesResponse>> Validate([FromBody] ValidatePickerRequest request)
    {
        var result = await _pickerService.ValidateAsync(request.Format, request.Fearless, request.AlternateSides);
        if (!result.IsOk) return this.Refusal(result);

        var id = result.Value!.Id;
        return Created($"/series/{id}", new CreateSeriesResponse(id));
    }
}
