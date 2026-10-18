using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Shameless.Api.Auth;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Requests.Clips;
using Shameless.Application.Clips;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>
/// Les Clips sur le site (ADR 0014) : ceux d'une Game et leur vidéo, conservée par l'Instance. Permis à
/// tout Utilisateur connecté.
/// </summary>
[ApiController]
public sealed class ClipsController : ControllerBase
{
    private readonly ClipsService _clips;

    public ClipsController(ClipsService clips) => _clips = clips;

    /// <summary>Liste paginée des clips pour le fil, avec filtres optionnels.</summary>
    [HttpGet("clips")]
    [ProducesResponseType<List<ClipResponse>>(StatusCodes.Status200OK)]
    public async Task<List<ClipResponse>> GetTous([FromQuery] bool? sansGame, [FromQuery] bool? masques = null,
        [FromQuery] string? auteur = null, [FromQuery] int? soireeId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 30) =>
        await _clips.GetTousAsync(sansGame, masques, auteur, soireeId, Math.Max(1, page), Math.Clamp(pageSize, 1, 100));

    /// <summary>Clips non masqués tirés au hasard pour le Fond.</summary>
    [HttpGet("clips/fond")]
    [ProducesResponseType<List<ClipResponse>>(StatusCodes.Status200OK)]
    public async Task<List<ClipResponse>> GetFond([FromQuery] int take = 10) =>
        await _clips.GetEligiblesAuFondAsync(Math.Clamp(take, 1, 20));

    [HttpPut("clips/{id:int}/fond")]
    [Authorize(Policy = Policies.Admin)]
    [ProducesResponseType<ClipResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ClipResponse>> SetFond(int id, [FromBody] FondClipRequest request)
    {
        var result = await _clips.MasquerDuFondAsync(id, request.Masque);
        if (!result.IsOk) return this.Refusal(result);
        return result.Value!;
    }

    /// <summary>Les clips rattachés à une game, du plus ancien au plus récent publié.</summary>
    [HttpGet("games/{gameId:int}/clips")]
    [ProducesResponseType<List<ClipResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<List<ClipResponse>>> GetDeLaGame(int gameId) =>
        await _clips.GetDeLaGameAsync(gameId) is { } clips
            ? clips
            : Problem(statusCode: StatusCodes.Status404NotFound, detail: "Cette game n'existe pas.");

    /// <summary>Les clips sans Game, à rattacher à la main, du plus ancien au plus récent publié.</summary>
    [HttpGet("clips/sans-game")]
    [ProducesResponseType<List<ClipResponse>>(StatusCodes.Status200OK)]
    public async Task<List<ClipResponse>> GetSansGame() => await _clips.GetSansGameAsync();

    /// <summary>
    /// Les games à proposer pour rattacher le clip : lancées la veille ou le jour de sa publication
    /// (fuseau de l'Instance), avant lui, de la plus récente à la plus ancienne.
    /// </summary>
    [HttpGet("clips/{id:int}/games-candidates")]
    [ProducesResponseType<List<GameCandidateDuClip>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<List<GameCandidateDuClip>>> GetGamesCandidates(int id) =>
        await _clips.GetGamesCandidatesAsync(id) is { } games
            ? games
            : Problem(statusCode: StatusCodes.Status404NotFound, detail: "Ce clip n'existe pas.");

    /// <summary>
    /// Rattache le clip à une Game, à la main (rattachement « manuel ») : un clip sans Game, ou dont la
    /// Game était fausse.
    /// </summary>
    [HttpPut("clips/{id:int}/game")]
    [ProducesResponseType<ClipResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ClipResponse>> Rattacher(int id, RattacherClipRequest request)
    {
        var result = await _clips.RattacherAsync(id, request.GameId);
        if (!result.IsOk) return this.Refusal(result);
        return result.Value!;
    }

    /// <summary>Retire le clip de sa Game : il revient parmi les clips sans Game.</summary>
    [HttpDelete("clips/{id:int}/game")]
    [ProducesResponseType<ClipResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<ClipResponse>> Detacher(int id)
    {
        var result = await _clips.DetacherAsync(id);
        if (!result.IsOk) return this.Refusal(result);
        return result.Value!;
    }

    /// <summary>
    /// La vidéo d'un clip, avec les requêtes de plage (<c>Range</c>) pour se déplacer dans la lecture.
    /// </summary>
    [HttpGet("clips/{id:int}/fichier")]
    [ProducesResponseType<FileResult>(StatusCodes.Status200OK, "video/mp4", "video/webm", "video/quicktime")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> GetFichier(int id) =>
        await _clips.GetFichierAsync(id) is { } fichier
            ? PhysicalFile(fichier.Chemin, fichier.ContentType, enableRangeProcessing: true)
            : Problem(statusCode: StatusCodes.Status404NotFound, detail: "Ce clip n'existe pas.");
}

public sealed record FondClipRequest(bool Masque);
