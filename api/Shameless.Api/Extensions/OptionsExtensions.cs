using Shameless.Application.BackgroundJobs;
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

        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection("Auth"));

        services.AddOptions<RefreshRanksJobOptions>()
            .Configure(job => job.Enabled = !string.IsNullOrWhiteSpace(configuration["Auth:Riot:ApiKey"]));

        return services;
    }
}