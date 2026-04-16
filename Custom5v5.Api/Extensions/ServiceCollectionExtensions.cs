using System.Text.Json.Serialization;
using Custom5v5.Api.Interfaces;
using Custom5v5.Api.Services;
using Custom5v5.Application.BackgroundJobs;
using Custom5v5.Application.Interfaces;
using Custom5v5.Application.Services;
using Custom5v5.Infrastructure.Data.Repositories;
using Custom5v5.Infrastructure.Repositories;
using Custom5v5.Infrastructure.Services;
using Microsoft.OpenApi.Models;

namespace Custom5v5.Api.Extensions;

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
        var origin  = configuration["AllowedOrigins"] ?? "http://localhost:3000";

        services.AddCors(options => 
        {
            options.AddDefaultPolicy(policy =>
            {
                policy.WithOrigins(origin)
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
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Custom5v5.Api", Version = "v1" });
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