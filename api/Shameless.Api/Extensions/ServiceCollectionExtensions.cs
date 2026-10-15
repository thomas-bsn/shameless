using System.Text.Json.Serialization;
using Shameless.Api.Interfaces;
using Shameless.Api.Services;
using Shameless.Application.BackgroundJobs;
using Shameless.Application.Interfaces;
using Shameless.Application.Services;
using Shameless.Infrastructure.Data.Repositories;
using Shameless.Infrastructure.Repositories;

namespace Shameless.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Background jobs
        services.AddHostedService<RefreshRanksJob>();

        // Repositories
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<ICustomGameRepository, CustomGameRepository>();
        services.AddScoped<ICustomGameParticipantRepository, CustomGameParticipantRepository>();
        services.AddScoped<IPlayerMonthlyCostRepository, PlayerMonthlyCostRepository>();
        services.AddScoped<IBestOfSeriesRepository, BestOfSeriesRepository>();

        // Services
        services.AddScoped<IPlayerService, PlayerService>();
        services.AddScoped<CustomGameService>();
        services.AddScoped<CostCalculationService>();

        // Singletons
        services.AddSingleton<IOAuthStateStore, OAuthStateStore>();

        return services;
    }

    public static IServiceCollection AddApiServices(this IServiceCollection services, IConfiguration configuration)
    {
        var origins = (configuration["AllowedOrigins"] ?? "http://localhost:3000")
            .Split(',', StringSplitOptions.RemoveEmptyEntries);

        services.AddCors(options => 
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(origins)
                    .AllowAnyHeader()
                    .AllowAnyMethod()
                    .AllowCredentials();
            });
        });

        services.AddControllers()
            .AddJsonOptions(o =>
                o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter())
            );

        services.AddOpenApi();
        services.AddProblemDetails();

        return services;
    }
}