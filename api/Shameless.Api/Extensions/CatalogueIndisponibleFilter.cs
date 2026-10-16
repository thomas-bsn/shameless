using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Shameless.Application.Interfaces;

namespace Shameless.Api.Extensions;

/// <summary>
/// Le seul endroit où un catalogue des champions indisponible devient une réponse : 503 en
/// ProblemDetails, pour toute action qui en a besoin (catalogue, actions de la Draft de champions).
/// </summary>
public sealed class CatalogueIndisponibleFilter(ProblemDetailsFactory problems) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not CatalogueIndisponibleException) return;

        var problem = problems.CreateProblemDetails(context.HttpContext,
            statusCode: StatusCodes.Status503ServiceUnavailable,
            detail: "Le catalogue des champions est indisponible pour le moment (Data Dragon).");
        context.Result = new ObjectResult(problem)
        {
            StatusCode = problem.Status,
            ContentTypes = { "application/problem+json" }
        };
        context.ExceptionHandled = true;
    }
}
