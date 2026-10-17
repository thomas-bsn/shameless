using Shameless.Application.BackgroundJobs;
using Shameless.Application.Bot;
using Shameless.Application.Clips;
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

        services.AddOptions<PublicationWebhooksBotOptions>()
            .Bind(configuration.GetSection(PublicationWebhooksBotOptions.Section));

        services.AddOptions<ClipsOptions>()
            .Bind(configuration.GetSection(ClipsOptions.Section));

        // Un fuseau inconnu fait échouer le démarrage plutôt que le premier dépôt de clip.
        services.AddOptions<InstanceOptions>()
            .Bind(configuration.GetSection(InstanceOptions.Section))
            .Validate(Fuseau.Existe, "Instance:TimeZone n'est pas un fuseau IANA connu (ex. Europe/Paris).")
            .ValidateOnStart();

        return services;
    }
}
internal static class Fuseau
{
    public static bool Existe(InstanceOptions instance)
    {
        try { instance.Fuseau(); return true; }
        catch (TimeZoneNotFoundException) { return false; }
        catch (InvalidTimeZoneException) { return false; }
    }
}
