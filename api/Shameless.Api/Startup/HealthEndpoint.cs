using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Shameless.Api.Startup;

public static class HealthEndpoint
{
    /// <summary>
    /// Même réponse que MapHealthChecks (texte « Healthy », 503 si Unhealthy), mais déclarée
    /// dans le contrat OpenAPI pour que le front l'appelle via le client généré.
    /// </summary>
    public static void MapHealth(this WebApplication app) =>
        app.MapGet("/health", async (HealthCheckService health, HttpContext http, CancellationToken ct) =>
            {
                var report = await health.CheckHealthAsync(ct);
                http.Response.Headers.CacheControl = "no-store, no-cache";
                return Results.Text(
                    report.Status.ToString(),
                    statusCode: report.Status == HealthStatus.Unhealthy
                        ? StatusCodes.Status503ServiceUnavailable
                        : StatusCodes.Status200OK);
            })
            .AllowAnonymous()
            .WithName("GetHealth")
            .Produces<string>(StatusCodes.Status200OK, "text/plain")
            .Produces<string>(StatusCodes.Status503ServiceUnavailable, "text/plain");
}
