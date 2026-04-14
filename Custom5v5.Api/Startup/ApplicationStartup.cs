using Custom5v5.Application.Interfaces;
using Custom5v5.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Custom5v5.Api.Startup;

public static class ApplicationStartup
{
    public static async Task<WebApplication> ConfigureApplicationAsync(this WebApplication app)
    {
        await app.RunDatabaseMigrationsAsync();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseCors();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapControllers();

        return app;
    }

    private static async Task RunDatabaseMigrationsAsync(this WebApplication app)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }
}