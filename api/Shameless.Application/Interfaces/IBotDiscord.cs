namespace Shameless.Application.Interfaces;

/// <summary>Les membres humains du salon vocal le plus peuplé du serveur, vus par le bot.</summary>
/// <param name="ChannelId">Le salon retenu, null si personne n'est en vocal.</param>
/// <param name="MemberIds">Les IDs Discord des membres présents, triés.</param>
public sealed record CandidatsVocauxDuBot(string? ChannelId, IReadOnlyList<long> MemberIds);

/// <summary>Un webhook à livrer au bot : l'enveloppe v1 du contrat, hors signature.</summary>
/// <param name="EventId">Identifiant stable : le même pour chaque tentative, le bot dédoublonne dessus.</param>
/// <param name="Type"><c>teams_formed</c>, <c>game_started</c> ou <c>game_completed</c>.</param>
/// <param name="Data">Le snapshot propre au type, sérialisé en snake_case.</param>
public sealed record WebhookDuBot(string EventId, string Type, DateTime OccurredAt, object Data);

/// <summary>L'issue d'une tentative de livraison d'un webhook.</summary>
public enum LivraisonWebhook
{
    /// <summary>Le bot l'a traité, ignoré, ou l'avait déjà traité (200).</summary>
    Livree,

    /// <summary>À retenter plus tard : déjà en cours (409), panne du bot (5xx), délai ou réseau.</summary>
    ARetenter,

    /// <summary>Refus définitif (signature refusée, enveloppe invalide…) : retenter ne changerait rien.</summary>
    Refusee
}

/// <summary>Le résultat d'une tentative de livraison, avec la raison quand elle n'est pas livrée.</summary>
public sealed record ResultatLivraison(LivraisonWebhook Issue, string? Raison = null);

/// <summary>
/// Le bot Discord de l'Instance, appelé par l'API avec son identité machine API → bot (ADR 0013) :
/// candidats vocaux du Picker et webhooks signés HMAC.
/// </summary>
public interface IBotDiscord
{
    /// <summary>
    /// Qui est en vocal. Lève <see cref="BotIndisponibleException"/> si le bot n'est pas configuré, ne
    /// répond pas, refuse la clé ou n'est pas prêt.
    /// </summary>
    Task<CandidatsVocauxDuBot> GetCandidatsVocauxAsync(CancellationToken cancellationToken = default);

    /// <summary>Une tentative de livraison du webhook, signée avec un timestamp frais.</summary>
    Task<ResultatLivraison> EnvoyerWebhookAsync(WebhookDuBot webhook, CancellationToken cancellationToken = default);
}

/// <summary>Le bot Discord est indisponible : l'API répond 503 à la requête qui en a besoin.</summary>
public sealed class BotIndisponibleException(string message, Exception? innerException = null)
    : Exception(message, innerException);
