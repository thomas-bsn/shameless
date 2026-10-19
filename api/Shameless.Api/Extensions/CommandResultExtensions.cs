using Microsoft.AspNetCore.Mvc;
using Shameless.Application.Services;

namespace Shameless.Api.Extensions;

public static class CommandResultExtensions
{
    /// <summary>Un refus de commande en ProblemDetails : 404, 400 (commande invalide) ou 409 (conflit d'état).</summary>
    public static ActionResult Refusal<T>(this ControllerBase controller, CommandResult<T> result) =>
        controller.Problem(
            statusCode: result.Status switch
            {
                CommandStatus.NotFound => StatusCodes.Status404NotFound,
                CommandStatus.Invalid => StatusCodes.Status400BadRequest,
                CommandStatus.TooManyRequests => StatusCodes.Status429TooManyRequests,
                _ => StatusCodes.Status409Conflict
            },
            detail: result.Detail);
}
