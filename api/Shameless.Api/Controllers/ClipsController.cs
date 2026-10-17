using Microsoft.AspNetCore.Mvc;
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

    /// <summary>Les clips rattachés à une game, du plus ancien au plus récent publié.</summary>
    [HttpGet("games/{gameId:int}/clips")]
    [ProducesResponseType<List<ClipResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<List<ClipResponse>>> GetDeLaGame(int gameId) =>
        await _clips.GetDeLaGameAsync(gameId) is { } clips
            ? clips
            : Problem(statusCode: StatusCodes.Status404NotFound, detail: "Cette game n'existe pas.");

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
