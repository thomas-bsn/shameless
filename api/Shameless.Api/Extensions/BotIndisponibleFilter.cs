using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Shameless.Application.Interfaces;

namespace Shameless.Api.Extensions;

/// <summary>
/// Le seul endroit où un bot Discord indisponible (non configuré, injoignable, pas prêt) devient une
/// réponse : 503 en ProblemDetails, avec la raison.
/// </summary>
public sealed class BotIndisponibleFilter(ProblemDetailsFactory problems) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not BotIndisponibleException indisponible) return;

        var problem = problems.CreateProblemDetails(context.HttpContext,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            detail: indisponible.Message);
        context.Result = new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { "application/problem+json" }
        };
        context.ExceptionHandled = true;
    }
}
