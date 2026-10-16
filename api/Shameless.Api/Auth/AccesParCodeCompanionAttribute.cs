using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Shameless.Application.Entities;
using Shameless.Application.Services;

namespace Shameless.Api.Auth;

/// <summary>
/// Ouvre une action au Companion, par le code Companion d'une Série porté dans le paramètre de route
/// <c>{code}</c> (même principe que les liens d'accès, ADR 0010). Code inconnu, renouvelé ou de Série
/// terminée : 404. L'action ne reçoit pas de session mais la Série du code
/// (<see cref="AccesParCodeCompanionExtensions.SerieDuCodeCompanion"/>).
/// </summary>
/// <remarks>
/// La Série vient du code, jamais de la requête : le Companion ne peut agir que sur sa Série. Un lien
/// d'accès n'est pas un code Companion, et inversement. Aucun cookie n'est posé.
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class AccesParCodeCompanionAttribute : Attribute, IAllowAnonymous, IAsyncActionFilter, IOrderedFilter
{
    public const string RouteParameter = "code";

    /// <summary>
    /// Laisse passer le code d'une Série terminée (mais pas un code inconnu ou renouvelé) : l'action
    /// refuse elle-même ce qui est nouveau, mais répond encore à ce qui a déjà été reçu, comme le
    /// renvoi d'un rapport déjà rattaché.
    /// </summary>
    public bool SerieTermineeAdmise { get; init; }

    /// <summary>Le code se vérifie avant la validation du corps : un code refusé répond 404 quel que soit le corps.</summary>
    public int Order => -3000;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var controller = (ControllerBase)context.Controller;
        var code = context.RouteData.Values[RouteParameter] as string;
        var codes = context.HttpContext.RequestServices.GetRequiredService<CodeCompanionService>();
        var series = code == null ? null : await codes.ResoudreAsync(code, SerieTermineeAdmise);

        if (series == null)
        {
            context.Result = controller.Problem(
                statusCode: StatusCodes.Status404NotFound,
                detail: "Ce code Companion n'existe pas, a été renouvelé ou sa Série est terminée : demandez le code à jour à l'organisateur.");
            return;
        }

        context.HttpContext.Items[typeof(AccesParCodeCompanionAttribute)] = series;
        await next();
    }
}

public static class AccesParCodeCompanionExtensions
{
    /// <summary>
    /// La Série (en cours sauf <see cref="AccesParCodeCompanionAttribute.SerieTermineeAdmise"/>, avec sa
    /// composition courante) du code Companion de la requête, dans une
    /// action marquée <see cref="AccesParCodeCompanionAttribute"/>.
    /// </summary>
    public static BestOfSeries SerieDuCodeCompanion(this ControllerBase controller) =>
        controller.HttpContext.Items[typeof(AccesParCodeCompanionAttribute)] as BestOfSeries
        ?? throw new InvalidOperationException($"L'action n'est pas marquée [{nameof(AccesParCodeCompanionAttribute)}].");
}
