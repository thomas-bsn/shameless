using Shameless.Application.BackgroundJobs;
using Shameless.Application.Bot;
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

        services.AddOptions<FermetureAutomatiqueSoireeOptions>()
            .Bind(configuration.GetSection(FermetureAutomatiqueSoireeOptions.Section));

        services.AddOptions<BotOptions>()
            .Bind(configuration.GetSection(BotOptions.Section));

        return services;
    }
}