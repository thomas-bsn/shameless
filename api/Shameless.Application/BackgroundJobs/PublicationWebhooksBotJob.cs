using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shameless.Application.Bot;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.BackgroundJobs;

/// <summary>Section de configuration <c>Bot:Publication</c>.</summary>
public sealed class PublicationWebhooksBotOptions
{
    public const string Section = "Bot:Publication";

    /// <summary>Fréquence de publication des webhooks en attente.</summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(3);
}

/// <summary>
/// Livre au bot Discord les webhooks de l'outbox (ADR 0013), hors des requêtes qui les produisent :
/// chaque événement dû est relu, signé avec un timestamp frais et envoyé ; livré, il ne part plus ;
/// sur 409, 5xx ou panne réseau, il est retenté plus tard avec le même <c>event_id</c> ; refusé
/// définitivement (4xx) ou trop vieux, il est abandonné.
/// </summary>
public sealed class PublicationWebhooksBotJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PublicationWebhooksBotJob> _logger;
    private readonly IOptions<PublicationWebhooksBotOptions> _publication;
    private readonly IOptions<BotOptions> _bot;
    private readonly TimeProvider _clock;

    public PublicationWebhooksBotJob(
        IServiceProvider services,
        ILogger<PublicationWebhooksBotJob> logger,
        IOptions<PublicationWebhooksBotOptions> publication,
        IOptions<BotOptions> bot,
        TimeProvider clock)
    {
        _services = services;
        _logger = logger;
        _publication = publication;
        _bot = bot;
        _clock = clock;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        if (!_bot.Value.WebhooksActifs) return;

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(_publication.Value.Interval, _clock, ct);
            try
            {
                await PublierAsync(ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "PublicationWebhooksBotJob failed");
            }
        }
    }

    /// <summary>
    /// Un passage du job : tente la livraison de chaque événement dû, dans l'ordre où ils sont survenus.
    /// S'arrête au premier dont l'issue n'a pas pu être enregistrée, pour garder l'ordre.
    /// </summary>
    public async Task PublierAsync(CancellationToken ct = default)
    {
        using var scope = _services.CreateScope();
        var evenements = scope.ServiceProvider.GetRequiredService<IEvenementBotRepository>();
        var bot = scope.ServiceProvider.GetRequiredService<IBotDiscord>();

        foreach (var evenement in await evenements.GetALivrerAsync(Maintenant()))
        {
            if (Snapshot(evenement) is not { } snapshot)
            {
                // Lien capitaine d'une Équipe sans Capitaine : il attend sa désignation (ou ses 24 h).
                evenement.Reporter(Maintenant(), "Aucun Capitaine désigné");
                if (!await evenements.TrySaveChangesAsync()) return;
                continue;
            }

            var webhook = new WebhookDuBot(evenement.EventId, evenement.TypeDuContrat, evenement.OccurredAt, snapshot);
            var resultat = await bot.EnvoyerWebhookAsync(webhook, ct);
            switch (resultat.Issue)
            {
                case LivraisonWebhook.Livree:
                    evenement.MarquerLivre(Maintenant());
                    break;
                case LivraisonWebhook.ARetenter:
                    evenement.Reporter(Maintenant(), resultat.Raison);
                    _logger.LogWarning("Webhook {EventId} à retenter ({Raison})", evenement.EventId, resultat.Raison);
                    break;
                default:
                    evenement.Abandonner(resultat.Raison);
                    _logger.LogError("Webhook {EventId} refusé par le bot ({Raison}) : abandonné", evenement.EventId, resultat.Raison);
                    break;
            }

            if (evenement.Statut == StatutEvenementBot.Abandonne && resultat.Issue == LivraisonWebhook.ARetenter)
                _logger.LogError("Webhook {EventId} non livré depuis {Duree} : abandonné", evenement.EventId, EvenementBot.DureeDeVie);
            if (!await evenements.TrySaveChangesAsync())
            {
                // Les suivants attendent le prochain passage : ils ne doivent pas arriver avant celui-ci.
                _logger.LogWarning("Webhook {EventId} : issue non enregistrée, publication interrompue", evenement.EventId);
                return;
            }
        }
    }

    private object? Snapshot(EvenementBot evenement) => evenement.Type switch
    {
        TypeEvenementBot.TeamsFormed => SnapshotsDuBot.TeamsFormed(evenement.Series!),
        TypeEvenementBot.CaptainLink => SnapshotsDuBot.CaptainLink(evenement.Series!, evenement.Equipe!, _bot.Value.WebUrl),
        TypeEvenementBot.SpectatorLink => SnapshotsDuBot.SpectatorLink(evenement.Series!, _bot.Value.WebUrl),
        TypeEvenementBot.GameStarted => SnapshotsDuBot.GameStarted(evenement.Game!, _bot.Value.WebUrl),
        _ => SnapshotsDuBot.GameCompleted(evenement.Game!, _bot.Value.WebUrl)
    };

    private DateTime Maintenant() => _clock.GetUtcNow().UtcDateTime;
}
