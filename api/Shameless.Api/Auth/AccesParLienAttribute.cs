using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shameless.Application.Entities;
using Shameless.Application.Services;

namespace Shameless.Api.Auth;

/// <summary>
/// Ouvre une action à un lien d'accès (ADR 0010), seule exception à la connexion Discord (ADR 0005).
/// La route porte le lien dans son paramètre <c>{lien}</c> ; l'action ne reçoit pas de session mais
/// l'accès que donne le lien (<see cref="AccesParLienExtensions.AccesParLien"/>) : sa Série, sa portée
/// et son Équipe. Lien inconnu, renouvelé ou Lien spectateur pas encore disponible : 404. Lien
/// spectateur sur une action réservée aux capitaines (<see cref="CapitaineRequis"/>) : 403.
/// </summary>
/// <remarks>
/// La Série et l'Équipe viennent du lien, jamais de la requête : une action par lien ne peut donc
/// toucher que sa Série, et une commande de capitaine que son Équipe. Aucun cookie n'est posé.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AccesParLienAttribute : Attribute, IAllowAnonymous, IAsyncActionFilter, IOrderedFilter
{
    public const string RouteParameter = "lien";

    /// <summary>
    /// Le lien se vérifie avant la validation du corps (filtre de <c>[ApiController]</c>, ordre -2000) :
    /// un lien inconnu répond 404 et un Lien spectateur 403, quel que soit le corps envoyé.
    /// </summary>
    public int Order => -3000;

    /// <summary>L'action est une commande de capitaine : un Lien spectateur est refusé.</summary>
    public bool CapitaineRequis { get; init; }

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = (ControllerBase)context.Controller;
        var secret = context.RouteData.Values[RouteParameter] as string;
        var liens = context.HttpContext.RequestServices.GetRequiredService<LiensAccesService>();
        var acces = secret == null ? null : await liens.ResoudreAsync(secret);

        if (acces == null)
        {
            context.Result = controller.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "Ce lien n'existe pas ou a été renouvelé : demandez le lien à jour à l'organisateur.");
            return;
        }
        if (CapitaineRequis && acces.Portee != PorteeLien.Capitaine)
        {
            context.Result = controller.Problem(
                statusCode: StatusCodes.Status403Forbidden,
                detail: "Le Lien spectateur permet seulement de regarder : il ne permet aucune action.");
            return;
        }

        context.HttpContext.Items[typeof(AccesParLien)] = acces;
        await next();
    }
}

public static class AccesParLienExtensions
{
    /// <summary>L'accès donné par le lien de la requête, dans une action marquée <see cref="AccesParLienAttribute"/>.</summary>
    public static AccesParLien AccesParLien(this ControllerBase controller) =>
        controller.HttpContext.Items[typeof(AccesParLien)] as AccesParLien
        ?? throw new InvalidOperationException($"L'action n'est pas marquée [{nameof(AccesParLienAttribute)}].");
}
