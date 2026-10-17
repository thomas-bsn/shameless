using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using Shameless.Application.Bot;
using Shameless.Application.Interfaces;
using Shameless.Application.Discord;

namespace Shameless.Infrastructure.Integrations.Bot;

/// <summary>
/// Client HTTP du bot Discord (contrat <c>docs/shameless.md</c> de harceleur_bot). Les candidats vocaux
/// portent la clé API → bot en <c>Authorization: Bearer</c> ; chaque webhook est signé HMAC-SHA256 sur
/// <c>"{timestamp}." + corps brut</c>, avec un timestamp frais à chaque tentative.
/// </summary>
public sealed class BotDiscordClient : IBotDiscord
{
    public const string CheminCandidatsVocaux = "internal/shameless/voice-candidates";
    public const string CheminWebhook = "internal/shameless/webhook";

    /// <summary>Le JSON du contrat du bot : snake_case, nulls omis (tout champ du snapshot est optionnel).</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly BotOptions _options;
    private readonly TimeProvider _clock;

    public BotDiscordClient(HttpClient http, IOptions<BotOptions> options, TimeProvider clock)
    {
        _http = http;
        _options = options.Value;
        _clock = clock;
    }

    public async Task<CandidatsVocauxDuBot> GetCandidatsVocauxAsync(CancellationToken cancellationToken = default)
    {
        if (!_options.AppelsActifs)
            throw new BotIndisponibleException("L'intégration du bot Discord n'est pas configurée sur cette Instance.");

        using var request = new HttpRequestMessage(HttpMethod.Get, Url(CheminCandidatsVocaux));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.OutboundApiKey);
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new BotIndisponibleException(response.StatusCode == HttpStatusCode.Unauthorized
                    ? "Le bot Discord refuse la clé de l'API : vérifiez SHAMELESS_BOT_API_KEY des deux côtés."
                    : "Le bot Discord n'est pas prêt : réessayez dans un instant.");

            var body = await response.Content.ReadFromJsonAsync<CandidatsVocauxJson>(Json, cancellationToken)
                       ?? throw new BotIndisponibleException("Le bot Discord a répondu une liste illisible.");
            return new CandidatsVocauxDuBot(
                body.ChannelId,
                (body.MemberIds ?? []).Select(DiscordId.Lire).OfType<DiscordId>().Select(id => id.Valeur).ToList());
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            throw new BotIndisponibleException("Le bot Discord ne répond pas : réessayez dans un instant.", ex);
        }
    }

    public async Task<ResultatLivraison> EnvoyerWebhookAsync(WebhookDuBot webhook, CancellationToken cancellationToken = default)
    {
        if (!_options.WebhooksActifs)
            return new ResultatLivraison(LivraisonWebhook.ARetenter, "webhooks non configurés");

        var corps = JsonSerializer.SerializeToUtf8Bytes(new
        {
            version = 1,
            event_id = webhook.EventId,
            type = webhook.Type,
            occurred_at = webhook.OccurredAt.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
            data = webhook.Data
        }, Json);
        var timestamp = _clock.GetUtcNow().ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        using var request = new HttpRequestMessage(HttpMethod.Post, Url(CheminWebhook));
        request.Content = new ByteArrayContent(corps) { Headers = { ContentType = new MediaTypeHeaderValue("application/json") } };
        request.Headers.Add("X-Shameless-Timestamp", timestamp);
        request.Headers.Add("X-Shameless-Signature", Signature(_options.WebhookSecret!, timestamp, corps));
        try
        {
            using var response = await _http.SendAsync(request, cancellationToken);
            var statut = (int)response.StatusCode;
            return statut switch
            {
                >= 200 and < 300 => new ResultatLivraison(LivraisonWebhook.Livree),
                409 or 408 or 429 or >= 500 => new ResultatLivraison(LivraisonWebhook.ARetenter, $"HTTP {statut}"),
                _ => new ResultatLivraison(LivraisonWebhook.Refusee, $"HTTP {statut}")
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            return new ResultatLivraison(LivraisonWebhook.ARetenter, ex.Message);
        }
    }

    /// <summary><c>sha256=</c> + hex(HMAC-SHA256(secret, <c>"{timestamp}."</c> + corps)).</summary>
    public static string Signature(string secret, string timestamp, byte[] corps)
    {
        var signe = Encoding.UTF8.GetBytes(timestamp + ".").Concat(corps).ToArray();
        return "sha256=" + Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), signe));
    }

    private Uri Url(string chemin) => new(new Uri(_options.Url!.TrimEnd('/') + "/"), chemin);

    private sealed record CandidatsVocauxJson(string? ChannelId, List<string>? MemberIds);
}
