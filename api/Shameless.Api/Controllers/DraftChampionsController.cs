using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Auth;
using Shameless.Api.Extensions;
using Shameless.Api.Models.Requests.ChampionDraft;
using Shameless.Api.Models.Responses.ChampionDraft;
using Shameless.Application.ChampionDraft;
using Shameless.Application.Entities;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>
/// La Draft de champions d'une game. L'organisateur connecté l'ouvre et la lit ; les capitaines la
/// jouent et la lisent par leur Lien capitaine, le spectateur la lit par le Lien spectateur (ADR 0010).
/// Passé l'échéance d'un tour (60 secondes), l'organisateur peut le jouer à la place de l'Équipe attendue.
/// </summary>
[ApiController]
public sealed class DraftChampionsController : ControllerBase
{
    private readonly DraftChampionsService _drafts;

    private readonly TimeProvider _clock;

    public DraftChampionsController(DraftChampionsService drafts, TimeProvider clock)
    {
        _drafts = drafts;
        _clock = clock;
    }

    private DraftChampionsResponse Reponse(DraftChampions draft) =>
        DraftChampionsResponse.From(draft, _clock.GetUtcNow().UtcDateTime);

    /// <summary>
    /// Sans connexion, par un Lien capitaine : demande à remettre la draft à zéro ; l'autre Capitaine a
    /// 60 secondes pour accepter ou refuser. Pas de draft, draft terminée ou demande déjà en attente : 409 ;
    /// Lien spectateur : 403.
    /// </summary>
    [HttpPost("liens/{lien}/draft-champions/reset")]
    [AccesParLien(CapitaineRequis = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> DemanderReset(string lien)
    {
        var acces = this.AccesParLien();
        var result = await _drafts.DemanderResetAsync(acces.SeriesId, acces.Equipe!);
        return result.IsOk ? NoContent() : this.Refusal(result);
    }

    /// <summary>
    /// Sans connexion, par le Lien capitaine de l'autre Équipe : accepte (la draft repart de zéro, même
    /// Game, les deux Capitaines doivent se remettre prêts) ou refuse (rien ne change). Pas de demande en
    /// attente ou expirée, ou demande de sa propre Équipe : 409 ; Lien spectateur : 403.
    /// </summary>
    [HttpPost("liens/{lien}/draft-champions/reset/reponse")]
    [AccesParLien(CapitaineRequis = true)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<IActionResult> RepondreReset(string lien, [FromBody] RepondreResetRequest request)
    {
        var acces = this.AccesParLien();
        var result = await _drafts.RepondreResetAsync(acces.SeriesId, acces.Equipe!, request.Accepte);
        return result.IsOk ? NoContent() : this.Refusal(result);
    }

    /// <summary>
    /// Ouvre la Draft de champions de la game en attente de la Série : Side connu et deux capitaines
    /// désignés. Une seule Draft de champions par game.
    /// </summary>
    [HttpPost("series/{id:int}/draft-champions")]
    [ProducesResponseType<DraftChampionsResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DraftChampionsResponse>> Ouvrir(int id)
    {
        var result = await _drafts.OuvrirAsync(id);
        if (!result.IsOk) return this.Refusal(result);

        var draft = Reponse(result.Value!);
        return Created($"/games/{draft.GameId}/draft-champions", draft);
    }

    /// <summary>La Draft de champions d'une game, en cours ou terminée.</summary>
    [HttpGet("games/{gameId:int}/draft-champions")]
    [ProducesResponseType<DraftChampionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<DraftChampionsResponse>> Get(int gameId)
    {
        var result = await _drafts.GetByGameAsync(gameId);
        if (!result.IsOk) return this.Refusal(result);

        return Reponse(result.Value!);
    }

    /// <summary>
    /// L'organisateur connecté joue le tour attendu (ban ou pick) à la place de l'Équipe attendue, une
    /// fois l'échéance du tour atteinte. Avant l'échéance, tour déjà joué ou hors tour, champion déjà
    /// banni ou pick, Draft terminée : 409 ; champion inconnu : 400.
    /// </summary>
    [HttpPost("games/{gameId:int}/draft-champions/actions")]
    [ProducesResponseType<DraftChampionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<DraftChampionsResponse>> JouerALaPlace(int gameId, [FromBody] JouerDraftChampionsRequest request) =>
        await JoueAsync(_drafts.JouerALaPlaceAsync(gameId, request.Tour, request.ChampionId));

    /// <summary>Sans connexion, par un Lien capitaine ou le Lien spectateur : la dernière Draft de champions de la Série.</summary>
    [HttpGet("liens/{lien}/draft-champions")]
    [AccesParLien]
    [ProducesResponseType<DraftChampionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<ActionResult<DraftChampionsResponse>> GetParLien(string lien)
    {
        var result = await _drafts.GetParLienAsync(this.AccesParLien());
        if (!result.IsOk) return this.Refusal(result);

        return Reponse(result.Value!);
    }

    /// <summary>
    /// Sans connexion, par un Lien capitaine : joue le tour attendu (ban ou pick) pour l'Équipe du lien.
    /// Tour déjà joué, hors tour ou de l'autre Équipe, champion déjà banni ou pick : 409 ; champion
    /// inconnu : 400 ; Lien spectateur : 403.
    /// </summary>
    [HttpPost("liens/{lien}/draft-champions/actions")]
    [AccesParLien(CapitaineRequis = true)]
    [ProducesResponseType<DraftChampionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<ActionResult<DraftChampionsResponse>> Jouer(string lien, [FromBody] JouerDraftChampionsRequest request)
    {
        // CapitaineRequis : le lien est un Lien capitaine, il porte son Équipe.
        var acces = this.AccesParLien();
        return await JoueAsync(_drafts.JouerParLienAsync(
            acces.SeriesId, new JoueurDraft.Capitaine(acces.Equipe!), request.Tour, request.ChampionId));
    }

    /// <summary>Sans connexion, par un Lien capitaine : met la draft en pause tout de suite, sans accord. Une pause par Capitaine et par draft, 5 minutes au plus ; déjà utilisée, déjà en pause ou terminée : 409 ; Lien spectateur : 403.</summary>
    [HttpPost("liens/{lien}/draft-champions/pause")]
    [AccesParLien(CapitaineRequis = true)]
    [ProducesResponseType<DraftChampionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DraftChampionsResponse>> MettreEnPause(string lien)
    {
        var acces = this.AccesParLien();
        return await JoueAsync(_drafts.MettreEnPauseAsync(acces.SeriesId, acces.Equipe!));
    }

    /// <summary>Par un Lien capitaine : demande la reprise anticipée (60 secondes pour répondre à l'autre Capitaine).</summary>
    [HttpPost("liens/{lien}/draft-champions/reprise")]
    [AccesParLien(CapitaineRequis = true)]
    [ProducesResponseType<DraftChampionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DraftChampionsResponse>> DemanderReprise(string lien)
    {
        var acces = this.AccesParLien();
        return await JoueAsync(_drafts.DemanderRepriseAsync(acces.SeriesId, acces.Equipe!));
    }

    /// <summary>Par le Lien capitaine de l'autre Équipe : accepte la reprise, immédiate avec le temps restant.</summary>
    [HttpPost("liens/{lien}/draft-champions/reprise/acceptation")]
    [AccesParLien(CapitaineRequis = true)]
    [ProducesResponseType<DraftChampionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DraftChampionsResponse>> AccepterReprise(string lien)
    {
        var acces = this.AccesParLien();
        return await JoueAsync(_drafts.AccepterRepriseAsync(acces.SeriesId, acces.Equipe!));
    }

    /// <summary>Par le Lien capitaine de l'autre Équipe : refuse la reprise, la pause continue.</summary>
    [HttpPost("liens/{lien}/draft-champions/reprise/refus")]
    [AccesParLien(CapitaineRequis = true)]
    [ProducesResponseType<DraftChampionsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    public async Task<ActionResult<DraftChampionsResponse>> RefuserReprise(string lien)
    {
        var acces = this.AccesParLien();
        return await JoueAsync(_drafts.RefuserRepriseAsync(acces.SeriesId, acces.Equipe!));
    }

    /// <summary>
    /// L'état de la Draft après une action jouée, ou le refus. Catalogue des champions indisponible :
    /// 503 (<see cref="CatalogueIndisponibleFilter"/>).
    /// </summary>
    private async Task<ActionResult<DraftChampionsResponse>> JoueAsync(Task<CommandResult<DraftChampions>> jouer)
    {
        var result = await jouer;
        if (!result.IsOk) return this.Refusal(result);

        return Reponse(result.Value!);
    }
}
