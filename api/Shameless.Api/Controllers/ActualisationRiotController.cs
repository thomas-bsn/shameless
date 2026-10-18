using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Auth;
using Shameless.Application.Riot;

namespace Shameless.Api.Controllers;

/// <summary>
/// L'actualisation des rangs et du rôle principal à la demande d'un Administrateur. Un seul lancement
/// à la fois, la tâche de 24 h comprise (verrou partagé dans <see cref="ActualisationRiot"/>).
/// </summary>
[ApiController]
[Route("admin/riot")]
[Authorize(Policy = Policies.Admin)]
public sealed class ActualisationRiotController(ActualisationRiot actualisation) : ControllerBase
{
    private const string DejaEnCours = "Une actualisation Riot est déjà en cours (celle d'un administrateur ou la tâche de 24 h). Réessaie quand elle est terminée.";
    private const string SansCle = "Aucune clé Riot n'est configurée sur cette Instance : l'actualisation est indisponible.";

    /// <summary>La disponibilité (clé Riot) et la progression de la dernière actualisation.</summary>
    [HttpGet]
    [ProducesResponseType<EtatActualisationRiot>(StatusCodes.Status200OK)]
    public EtatActualisationRiot GetEtat() => actualisation.Etat();

    [HttpPost("joueurs/{playerId:int}")]
    [ProducesResponseType<ResultatActualisationJoueur>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> ActualiserUnJoueur(int playerId)
    {
        if (!actualisation.Disponible) return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: SansCle);

        var (libre, resultat) = await actualisation.ActualiserJoueurAsync(playerId);
        if (!libre) return Problem(statusCode: StatusCodes.Status409Conflict, detail: DejaEnCours);
        return resultat is null
            ? Problem(statusCode: StatusCodes.Status404NotFound, detail: "Joueur introuvable.")
            : Ok(resultat);
    }

    /// <summary>Lance l'actualisation de tous les joueurs en tâche de fond ; la progression se lit avec GET.</summary>
    [HttpPost("tous")]
    [ProducesResponseType<EtatActualisationRiot>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, "application/problem+json")]
    public async Task<IActionResult> ActualiserTous()
    {
        if (!actualisation.Disponible) return Problem(statusCode: StatusCodes.Status503ServiceUnavailable, detail: SansCle);
        if (!await actualisation.DemarrerTousAsync()) return Problem(statusCode: StatusCodes.Status409Conflict, detail: DejaEnCours);
        return Accepted(actualisation.Etat());
    }
}
