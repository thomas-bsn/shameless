using System.Net;
using Polly;
using Polly.Extensions.Http;
using Shameless.Application.Riot;

namespace Shameless.Infrastructure.Integrations.Riot;

/// <summary>
/// Les politiques de reprise du client HTTP Riot. Chaque nouvelle tentative compte dans la limite de la clé
/// (100 requêtes / 2 min en dev) : on ne réessaie donc que ce qui peut passer.
/// </summary>
public static class RiotHttpPolicies
{
    /// <summary>
    /// Reprend les pannes (5xx, 408) avec une attente croissante, et une 429 après le Retry-After de Riot
    /// quand il est court. Une 403 ou 404 n'est jamais reprise. <paramref name="attente"/> remplace l'attente
    /// croissante (2, 4, 8 s) ; les tests la raccourcissent.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> Retry(Func<int, TimeSpan>? attente = null)
    {
        attente ??= n => TimeSpan.FromSeconds(Math.Pow(2, n));
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .OrResult(msg => msg.StatusCode == HttpStatusCode.TooManyRequests
                             && (DelaiDeRiot(msg) ?? TimeSpan.Zero) <= RiotException.AttenteMaximale)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: (n, resultat, _) => DelaiDeRiot(resultat.Result) ?? attente(n),
                onRetryAsync: (_, _, _, _) => Task.CompletedTask);
    }

    /// <summary>Ouvre après 5 pannes de suite ; une 429 ou une 403 n'est pas une panne de Riot.</summary>
    public static IAsyncPolicy<HttpResponseMessage> CircuitBreaker() =>
        HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30));

    /// <summary>Le délai que Riot demande par Retry-After (en secondes ou en date) ; null hors d'une 429 sans l'en-tête.</summary>
    internal static TimeSpan? DelaiDeRiot(HttpResponseMessage? reponse)
    {
        if (reponse is not { StatusCode: HttpStatusCode.TooManyRequests }) return null;
        var retryAfter = reponse.Headers.RetryAfter;
        return retryAfter?.Delta ?? (retryAfter?.Date is { } d ? d - DateTimeOffset.UtcNow : null);
    }
}
