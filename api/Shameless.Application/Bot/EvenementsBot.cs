using Microsoft.Extensions.Options;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Bot;

/// <summary>
/// Le point d'émission des webhooks vers le bot Discord (ADR 0013) : chaque commande y signale son
/// événement, qui est ajouté à l'outbox et s'enregistre avec la commande, ou pas du tout. Sans webhooks
/// configurés (<see cref="BotOptions.WebhooksActifs"/>), rien n'est enregistré.
/// </summary>
public sealed class EvenementsBot
{
    private readonly IEvenementBotRepository _evenements;
    private readonly BotOptions _options;
    private readonly TimeProvider _clock;

    public EvenementsBot(IEvenementBotRepository evenements, IOptions<BotOptions> options, TimeProvider clock)
    {
        _evenements = evenements;
        _options = options.Value;
        _clock = clock;
    }

    /// <summary>Une Série est créée (validation du Picker, création directe, Revanche) : ses Équipes sont formées.</summary>
    public void TeamsFormed(BestOfSeries series) => Ajouter(EvenementBot.TeamsFormed(series, Maintenant()));

    /// <summary>Une Game est lancée : c'est son début (pronostics clos, fenêtre de présence du bot ouverte).</summary>
    public void GameStarted(CustomGame game) => Ajouter(EvenementBot.GameStarted(game, Maintenant()));

    /// <summary>Une Game est terminée, quelle que soit la source du résultat (saisie, Companion, revue acceptée).</summary>
    public void GameCompleted(CustomGame game) => Ajouter(EvenementBot.GameCompleted(game, Maintenant()));

    private void Ajouter(EvenementBot evenement)
    {
        if (_options.WebhooksActifs) _evenements.Add(evenement);
    }

    private DateTime Maintenant() => _clock.GetUtcNow().UtcDateTime;
}
