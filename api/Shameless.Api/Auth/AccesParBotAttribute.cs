using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Shameless.Application.Bot;

namespace Shameless.Api.Auth;

/// <summary>
/// Ouvre une route au bot Discord de l'Instance, par sa clé (<see cref="BotOptions.ApiKey"/>) en
/// <c>Authorization: Bearer</c> : l'identité machine bot → API (ADR 0013). Clé absente, fausse ou non
/// configurée : 401. Aucune session n'est lue ni posée.
/// </summary>
/// <remarks>
/// Ce n'est pas un schéma d'authentification : le bot n'est jamais un « utilisateur connecté » et la
/// clé n'ouvre donc aucune route des Utilisateurs. Inversement, une session n'ouvre aucune route du bot.
/// La clé se change (ou se révoque, vide) par l'env, sans changement de code.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class AccesParBotAttribute : Attribute, IAllowAnonymous, IAuthorizationFilter, IOrderedFilter
{
    /// <summary>La clé se vérifie avant tout autre filtre, et avant la validation du corps.</summary>
    public int Order => -3000;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var cle = context.HttpContext.RequestServices.GetRequiredService<IOptions<BotOptions>>().Value.ApiKey;
        if (!string.IsNullOrWhiteSpace(cle) && PresenteLaCle(context.HttpContext.Request, cle)) return;

        context.Result = new ObjectResult(new ProblemDetails
        {
            Status = StatusCodes.Status401Unauthorized,
            Title = "Unauthorized",
            Detail = "Route réservée au bot Discord de l'Instance : sa clé est absente ou invalide."
        })
        {
            StatusCode = StatusCodes.Status401Unauthorized,
            ContentTypes = { "application/problem+json" }
        };
    }

    private static bool PresenteLaCle(HttpRequest request, string cle)
    {
        var header = request.Headers.Authorization.ToString();
        const string prefixe = "Bearer ";
        if (!header.StartsWith(prefixe, StringComparison.OrdinalIgnoreCase)) return false;
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(header[prefixe.Length..].Trim()),
            Encoding.UTF8.GetBytes(cle));
    }
}
