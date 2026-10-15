using Shameless.Infrastructure.Options;

namespace Shameless.Api.Extensions;

public static class OptionsExtensions
{
    public static IServiceCollection AddApplicationOptions(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<RiotApiOptions>()
            .Bind(configuration.GetSection("Auth:Riot"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DiscordOAuthOptions>()
            .Bind(configuration.GetSection("Auth:Discord"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<JwtOptions>()
            .Bind(configuration.GetSection("Auth:Jwt"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<DatabaseOptions>()
            .Bind(configuration.GetSection("Database"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}