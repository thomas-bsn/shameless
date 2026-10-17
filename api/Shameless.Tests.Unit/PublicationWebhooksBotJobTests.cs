using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shameless.Application.BackgroundJobs;
using Shameless.Application.Bot;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Tests.Unit;

/// <summary>
/// Le job de publication livre les webhooks dans l'ordre où ils sont survenus : si l'issue d'une
/// livraison ne peut pas être enregistrée, il s'arrête là pour ne pas livrer les suivants avant elle.
/// </summary>
public class PublicationWebhooksBotJobTests
{
    private sealed class Outbox(List<EvenementBot> evenements, params bool[] enregistrements) : IEvenementBotRepository
    {
        private readonly Queue<bool> _enregistrements = new(enregistrements);

        public void Add(EvenementBot evenement) => evenements.Add(evenement);

        public Task<List<EvenementBot>> GetALivrerAsync(DateTime maintenant) =>
            Task.FromResult(evenements.Where(e => e.Statut == StatutEvenementBot.ALivrer).ToList());

        public Task<bool> TrySaveChangesAsync() =>
            Task.FromResult(_enregistrements.Count == 0 || _enregistrements.Dequeue());
    }

    private sealed class Bot : IBotDiscord
    {
        public List<string> Livres { get; } = [];

        public Task<CandidatsVocauxDuBot> GetCandidatsVocauxAsync(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<ResultatLivraison> EnvoyerWebhookAsync(WebhookDuBot webhook, CancellationToken cancellationToken = default)
        {
            Livres.Add(webhook.EventId);
            return Task.FromResult(new ResultatLivraison(LivraisonWebhook.Livree));
        }
    }

    private static EvenementBot TeamsFormed(int seriesId)
    {
        var evenement = EvenementBot.TeamsFormed(new BestOfSeries { Id = seriesId, Format = 1 }, DateTime.UtcNow);
        evenement.SeriesId = seriesId;
        return evenement;
    }

    private static PublicationWebhooksBotJob Job(IEvenementBotRepository outbox, IBotDiscord bot) =>
        new(
            new ServiceCollection().AddSingleton(outbox).AddSingleton(bot).BuildServiceProvider(),
            NullLogger<PublicationWebhooksBotJob>.Instance,
            Options.Create(new PublicationWebhooksBotOptions()),
            Options.Create(new BotOptions()),
            TimeProvider.System);

    [Fact]
    public async Task Si_l_issue_d_une_livraison_n_est_pas_enregistree_les_evenements_suivants_attendent_le_prochain_passage()
    {
        var outbox = new Outbox([TeamsFormed(1), TeamsFormed(2), TeamsFormed(3)], true, false);
        var bot = new Bot();

        await Job(outbox, bot).PublierAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["teams_formed:series:1", "teams_formed:series:2"], bot.Livres);
    }
}
