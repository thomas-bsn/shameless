using Shameless.Api.Extensions;
using Shameless.Api.Models.Requests.Series;
using Shameless.Api.Models.Responses.Series;
using Shameless.Application.DTOs.Match;
using Shameless.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Shameless.Api.Controllers;

[ApiController]
[Route("series")]
public sealed class SeriesController : ControllerBase
{
    private readonly SeriesService _seriesService;

    public SeriesController(SeriesService seriesService)
    {
        _seriesService = seriesService;
    }

    /// <summary>Crée une Série dans la Soirée ouverte.</summary>
    [HttpPost]
    [ProducesResponseType<CreateSeriesResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CreateSeriesResponse>> Create([FromBody] CreateSeriesRequest request)
    {
        var result = await _seriesService.CreateAsync(
            request.TeamA, request.TeamB, request.Format, request.Fearless, request.AlternateSides);
        if (!result.IsOk) return this.Refusal(result);

        var id = result.Value!.Id;
        return Created($"/series/{id}", new CreateSeriesResponse(id));
    }

    /// <summary>Lance la prochaine game d'une Série en cours, avec le Side de chaque Équipe.</summary>
    [HttpPost("{id:int}/games")]
    [ProducesResponseType<SeriesGameResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SeriesGameResponse>> PlayGame(int id, [FromBody] PlayGameRequest request)
    {
        var result = await _seriesService.PlayNextGameAsync(id, request.TeamASide);
        if (!result.IsOk) return this.Refusal(result);

        return Created($"/series/{id}", SeriesGameResponse.From(result.Value!));
    }

    [HttpGet("active")]
    [ProducesResponseType<ActiveSeriesResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ActiveSeriesResponse>> GetActiveSeries() =>
        new ActiveSeriesResponse(await _seriesService.GetEnCoursAsync());

    [HttpGet("{id:int}")]
    [ProducesResponseType<SeriesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SeriesResponse>> GetSeriesStatus(int id)
    {
        var series = await _seriesService.GetAsync(id);
        if (series == null) return NotFound();
        return series;
    }

    /// <summary>
    /// Arrête une Série en cours : annulée (effacée) si aucune game n'est terminée, sinon interrompue
    /// sans vainqueur, ses games terminées conservées.
    /// </summary>
    [HttpPost("{id:int}/stop")]
    [ProducesResponseType<StopSeriesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<StopSeriesResponse>> Stop(int id)
    {
        var result = await _seriesService.StopAsync(id);
        if (!result.IsOk) return this.Refusal(result);

        return new StopSeriesResponse(id, result.Value);
    }
}
