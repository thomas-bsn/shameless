using Microsoft.EntityFrameworkCore;
using Shameless.Infrastructure.Data;

namespace Shameless.Api.Extensions;

public static class DatabaseExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Variable d'environnement ConnectionStrings__Shameless
        var connection = configuration.GetConnectionString("Shameless")
            ?? throw new InvalidOperationException("Missing ConnectionStrings:Shameless configuration.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connection).UseSnakeCaseNamingConvention());

        services.AddHealthChecks().AddDbContextCheck<AppDbContext>("postgres");

        return services;
    }
}
