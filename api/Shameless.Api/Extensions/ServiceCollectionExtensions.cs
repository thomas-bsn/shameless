using System.Text.Json.Serialization;
using Shameless.Api.Interfaces;
using Shameless.Api.Services;
using Shameless.Application.BackgroundJobs;
using Shameless.Application.Interfaces;
using Shameless.Application.Services;
using Shameless.Infrastructure.Data.Repositories;
using Shameless.Infrastructure.Repositories;
using Shameless.Infrastructure.Services;
using Microsoft.OpenApi.Models;

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
        services.AddSingleton<IJwtIssuer, JwtIssuer>();

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

        services.AddEndpointsApiExplorer();
        services.AddSwaggerConfiguration();

        return services;
    }

    private static IServiceCollection AddSwaggerConfiguration(this IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Shameless.Api", Version = "v1" });
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header
            });
            options.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                    },
                    Array.Empty<string>()
                }
            });
        });

        return services;
    }
}