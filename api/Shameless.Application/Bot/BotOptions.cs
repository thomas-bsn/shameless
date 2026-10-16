namespace Shameless.Application.Bot;

/// <summary>
/// Section de configuration <c>Bot</c> : l'intégration avec le bot Discord de l'Instance (ADR 0013).
/// Tout est optionnel : sans configuration, l'Instance démarre, aucun webhook n'est émis, les candidats
/// vocaux sont indisponibles et les routes <c>/bot/…</c> refusent tout.
/// </summary>
public sealed class BotOptions
{
    public const string Section = "Bot";

    /// <summary>URL de base du bot (son API interne derrière le reverse proxy), ex. <c>https://bot.example.org</c>.</summary>
    public string? Url { get; set; }

    /// <summary>
    /// Identité machine API → bot : credential présenté au bot en <c>Authorization: Bearer</c>
    /// (<c>SHAMELESS_BOT_API_KEY</c> côté bot).
    /// </summary>
    public string? OutboundApiKey { get; set; }

    /// <summary>Secret HMAC des webhooks API → bot (<c>SHAMELESS_WEBHOOK_SECRET</c>), distinct des clés.</summary>
    public string? WebhookSecret { get; set; }

    /// <summary>
    /// Identité machine bot → API : clé que le bot présente à l'API en <c>Authorization: Bearer</c> sur
    /// les routes <c>/bot/…</c>. Vide : ces routes refusent tout. Distincte des deux secrets précédents.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>URL publique du site, pour les liens envoyés au bot (récap, Série). Vide : pas de lien.</summary>
    public string? WebUrl { get; set; }

    /// <summary>Délai max d'un appel au bot.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(10);

    /// <summary>Les webhooks sont émis : l'URL du bot et le secret HMAC sont configurés.</summary>
    public bool WebhooksActifs => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(WebhookSecret);

    /// <summary>L'API peut appeler le bot (candidats vocaux) : l'URL du bot et sa clé sont configurées.</summary>
    public bool AppelsActifs => !string.IsNullOrWhiteSpace(Url) && !string.IsNullOrWhiteSpace(OutboundApiKey);
}
