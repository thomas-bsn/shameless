namespace Shameless.Api.Startup;

public static class ApplicationStartup
{
    public static WebApplication ConfigureApplication(this WebApplication app)
    {
        // Hors développement : ProblemDetails générique, sans stack trace.
        if (!app.Environment.IsDevelopment())
            app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();
        app.MapHealthChecks("/health").AllowAnonymous();

        if (app.Environment.IsDevelopment())
            app.MapOpenApi().AllowAnonymous();

        return app;
    }
}