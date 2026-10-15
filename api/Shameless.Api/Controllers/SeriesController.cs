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

    /// <summary>
    /// Remplace un joueur d'une Équipe entre deux games d'une Série en cours. La composition
    /// courante change pour la prochaine game ; les games jouées et le score restent inchangés.
    /// </summary>
    [HttpPost("{id:int}/replacements")]
    [ProducesResponseType<SeriesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SeriesResponse>> ReplacePlayer(int id, [FromBody] ReplacePlayerRequest request)
    {
        var result = await _seriesService.ReplacePlayerAsync(id, request.ReplacedPlayerId, request.ReplacementPlayerId);
        if (!result.IsOk) return this.Refusal(result);

        return result.Value!;
    }

    /// <summary>
    /// Désigne le Capitaine d'une Équipe ("A" | "B") d'une Série en cours : à la main, au hasard ou le
    /// plus gros cost de l'Équipe. Remplace le capitaine précédent de cette Équipe.
    /// </summary>
    [HttpPut("{id:int}/capitaines/{equipe}")]
    [ProducesResponseType<SeriesResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<SeriesResponse>> DesignerCapitaine(
        int id, string equipe, [FromBody] DesignerCapitaineRequest request)
    {
        var result = await _seriesService.DesignerCapitaineAsync(id, equipe, request.Designation, request.PlayerId);
        if (!result.IsOk) return this.Refusal(result);

        return result.Value!;
    }

    /// <summary>
    /// Lance la Revanche d'une Série terminée : une nouvelle Série dans la même Soirée, encore
    /// ouverte, entre les mêmes Équipes dans leur dernière composition.
    /// </summary>
    [HttpPost("{id:int}/rematch")]
    [ProducesResponseType<CreateSeriesResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<CreateSeriesResponse>> Rematch(int id, [FromBody] RematchRequest request)
    {
        var result = await _seriesService.CreateRevancheAsync(id, request.Format, request.Fearless, request.AlternateSides);
        if (!result.IsOk) return this.Refusal(result);

        var revancheId = result.Value!.Id;
        return Created($"/series/{revancheId}", new CreateSeriesResponse(revancheId));
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
