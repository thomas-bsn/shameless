using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shameless.Api.Auth;
using Shameless.Api.Models.Responses.Auth;
using Shameless.Application.Discord;
using Shameless.Application.Services;

namespace Shameless.Api.Controllers;

/// <summary>
/// Les rôles des Utilisateurs (ADR 0017). Un Administrateur nomme et retire des Administrateurs ; il ne
/// touche pas un Superadministrateur et ne se retire pas lui-même. Un Superadministrateur ne se crée ni ne
/// se retire ici : seulement par la commande de l'image de l'API.
/// </summary>
[ApiController]
[Route("utilisateurs")]
[Authorize(Policy = Policies.Admin)]
public sealed class UtilisateursController(RolesService roles) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<List<UtilisateurResponse>>(StatusCodes.Status200OK)]
    public async Task<List<UtilisateurResponse>> List() =>
        (await roles.ListAsync()).Select(UtilisateurResponse.From).ToList();

    /// <summary>Promeut un Utilisateur (qui s'est déjà connecté) en Administrateur. Sans effet s'il l'est déjà.</summary>
    [HttpPut("{discordId}/administrateur")]
    [ProducesResponseType<UtilisateurResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Promouvoir(string discordId) =>
        DiscordId.Lire(discordId) is not { } id
            ? IdInvalide()
            : await Reponse(await roles.PromoteAdministratorAsync(User.DiscordIdOrNull()!.Value, id.Valeur), id.Valeur);

    /// <summary>Retire le rôle d'Administrateur à un autre Administrateur.</summary>
    [HttpDelete("{discordId}/administrateur")]
    [ProducesResponseType<UtilisateurResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, "application/problem+json")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, "application/problem+json")]
    public async Task<IActionResult> Retirer(string discordId) =>
        DiscordId.Lire(discordId) is not { } id
            ? IdInvalide()
            : await Reponse(await roles.RemoveAdministratorAsync(User.DiscordIdOrNull()!.Value, id.Valeur), id.Valeur);

    // Les snowflakes sont en texte dans les contrats : trop grands pour un nombre JavaScript.
    private ObjectResult IdInvalide() =>
        Problem(statusCode: StatusCodes.Status400BadRequest, detail: "discordId est un identifiant Discord (snowflake, en texte).");

    private async Task<IActionResult> Reponse(RoleResult result, long discordId) => result switch
    {
        RoleResult.Ok => Ok(UtilisateurResponse.From((await roles.ListAsync()).Single(u => u.DiscordId == discordId))),
        RoleResult.UserNotFound => Problem(statusCode: StatusCodes.Status404NotFound,
            detail: "Cet utilisateur ne s'est jamais connecté à Shameless."),
        _ => Problem(statusCode: StatusCodes.Status403Forbidden,
            detail: "Un Administrateur ne touche pas un Superadministrateur et ne se retire pas son propre rôle.")
    };
}
