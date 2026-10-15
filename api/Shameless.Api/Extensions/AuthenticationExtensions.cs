using Shameless.Api.Auth;
using Shameless.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using Shameless.Application.Interfaces;
using Shameless.Infrastructure.Options;
using Shameless.Infrastructure.Integrations.Riot;
using Shameless.Infrastructure.Integrations.Discord;
using Polly;
using Polly.Extensions.Http;

namespace Shameless.Api.Extensions;

public static class AuthenticationExtensions
{
    public const string DefaultScheme = "Shameless";

    /// <summary>
    /// Session par cookie posé à la fin du login Discord. Le scheme par défaut est un
    /// sélecteur : un futur jeton révocable (Companion, bot) s'y branchera sans refonte.
    /// </summary>
    public static IServiceCollection AddSessionAuthentication(this IServiceCollection services)
    {
        services.AddAuthentication(DefaultScheme)
            .AddPolicyScheme(DefaultScheme, DefaultScheme, options =>
            {
                options.ForwardDefaultSelector = _ => CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie();

        services.AddOptions<CookieAuthenticationOptions>(CookieAuthenticationDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((options, auth) =>
            {
                options.Cookie.Name = "shameless_session";
                options.Cookie.HttpOnly = true;
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromDays(auth.Value.SessionLifetimeDays);
                options.SlidingExpiration = true;
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddSingleton<IAuthorizationHandler, AdminHandler>();
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(Policies.Admin, policy => policy.RequireAuthenticatedUser().AddRequirements(new AdminRequirement()));

        return services;
    }

    public static IServiceCollection AddHttpClients(this IServiceCollection services)
    {
        services.AddHttpClient<IRiotService, RiotService>((sp, client) =>
            {
                var riot = sp.GetRequiredService<IOptions<RiotApiOptions>>().Value;
                client.BaseAddress = new Uri(riot.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(riot.TimeoutSeconds);
                client.DefaultRequestHeaders.Add("X-Riot-Token", riot.ApiKey);
            })
            .AddPolicyHandler(GetRetryPolicy())
            .AddPolicyHandler(GetCircuitBreakerPolicy());

        services.AddHttpClient<IDiscordOAuthClient, DiscordOAuthClient>((sp, client) =>
            {
                var discord = sp.GetRequiredService<IOptions<DiscordOAuthOptions>>().Value;
                client.BaseAddress = new Uri(discord.BaseUrl);
            })
            .AddPolicyHandler(GetRetryPolicy());

        return services;
    }
    
    private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError() // 5xx + 408
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests) // 429
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // 2s, 4s, 8s
                onRetry: (outcome, timespan, retryAttempt, context) =>
                {
                    Console.WriteLine($"Retry {retryAttempt} after {timespan.TotalSeconds}s due to {outcome.Result?.StatusCode}");
                }
            );
    }

    private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30)
            );
    }
}