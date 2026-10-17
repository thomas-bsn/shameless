using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Shameless.Api.Auth;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Requests.Clips;
using Shameless.Application.Clips;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>
/// Le dépôt des Clips de <c>#clips</c> par le bot Discord, ouvert par sa clé seule (ADR 0013, 0014).
/// </summary>
[ApiController]
[Route("bot/clips")]
[AccesParBot]
[Tags("Bot")]
public sealed class BotClipsController : ControllerBase
{
    private readonly ClipsService _clips;
    private readonly IOptions<ClipsOptions> _options;

    public BotClipsController(ClipsService clips, IOptions<ClipsOptions> options)
    {
        _clips = clips;
        _options = options;
    }

    /// <summary>
    /// Dépose une vidéo téléchargée de <c>#clips</c> : l'API la conserve et la rattache à l'unique Game
    /// plausible du jour de publication, sinon le clip reste sans Game. Idempotent sur
    /// <c>attachmentId</c> : 201 au premier dépôt, 200 avec le même clip ensuite (la vidéo n'est pas
    /// réécrite). 413 au-delà de la taille maximale d'un clip.
    /// </summary>
    [HttpPost]
    [LimiteDesClips]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ClipDuBot>(StatusCodes.Status201Created)]
    [ProducesResponseType<ClipDuBot>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status413PayloadTooLarge, "application/problem+json")]
    public async Task<ActionResult<ClipDuBot>> Deposer([FromForm] DepotDeClipRequest request, CancellationToken ct)
    {
        var (depot, refus) = request.ToDepot();
        if (depot is null) return Problem(statusCode: StatusCodes.Status400BadRequest, detail: refus);

        var max = _options.Value.MaxFileSizeBytes;
        if (depot.Taille > max)
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge,
                detail: $"Un clip ne dépasse pas {max / (1024 * 1024)} Mo.");

        await using var video = request.Fichier!.OpenReadStream();
        var result = await _clips.DeposerAsync(depot, video, ct);
        if (!result.IsOk) return this.Refusal(result);

        var (clip, nouveau) = result.Value;
        return nouveau ? StatusCode(StatusCodes.Status201Created, clip) : clip;
    }
}
