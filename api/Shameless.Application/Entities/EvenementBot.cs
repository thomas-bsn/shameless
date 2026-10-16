namespace Shameless.Application.Entities;

/// <summary>
/// Un événement à livrer au bot Discord en webhook (outbox, ADR 0013). Enregistré dans la même
/// transaction que la commande qui le produit, puis livré par le job de publication, retenté jusqu'à
/// livraison. Son <see cref="EventId"/> est stable : le bot dédoublonne dessus.
/// </summary>
/// <remarks>
/// Le snapshot n'est pas figé : il est relu de la Série ou de la Game à chaque tentative. Un événement
/// disparaît avec sa Série ou sa Game (Série annulée, game en attente supprimée, ADR 0009).
/// </remarks>
public class EvenementBot
{
    /// <summary>Au-delà, un événement non livré est abandonné : ses effets Discord n'ont plus de sens.</summary>
    public static readonly TimeSpan DureeDeVie = TimeSpan.FromHours(24);

    public long Id { get; set; }
    public TypeEvenementBot Type { get; set; }

    /// <summary>La Série de <see cref="TypeEvenementBot.TeamsFormed"/>, null sinon.</summary>
    public int? SeriesId { get; set; }
    public BestOfSeries? Series { get; set; }

    /// <summary>La Game de <see cref="TypeEvenementBot.GameStarted"/> et <see cref="TypeEvenementBot.GameCompleted"/>, null sinon.</summary>
    public int? GameId { get; set; }
    public CustomGame? Game { get; set; }

    public DateTime OccurredAt { get; set; }
    public StatutEvenementBot Statut { get; set; } = StatutEvenementBot.ALivrer;
    public int Tentatives { get; set; }

    /// <summary>Pas de tentative avant cette date (attente croissante entre les échecs).</summary>
    public DateTime ProchaineTentative { get; set; }
    public DateTime? LivreLe { get; set; }

    /// <summary>La raison du dernier échec, pour le diagnostic.</summary>
    public string? DerniereErreur { get; set; }

    /// <summary>Le type du contrat du bot : <c>teams_formed</c>, <c>game_started</c> ou <c>game_completed</c>.</summary>
    public string TypeDuContrat => Type switch
    {
        TypeEvenementBot.TeamsFormed => "teams_formed",
        TypeEvenementBot.GameStarted => "game_started",
        _ => "game_completed"
    };

    /// <summary>Stable pour toutes les tentatives : <c>{type}:series:{id}</c> ou <c>{type}:game:{id}</c>.</summary>
    public string EventId => SeriesId is { } seriesId
        ? $"{TypeDuContrat}:series:{seriesId}"
        : $"{TypeDuContrat}:game:{GameId}";

    public static EvenementBot TeamsFormed(BestOfSeries series, DateTime maintenant) =>
        new() { Type = TypeEvenementBot.TeamsFormed, Series = series, OccurredAt = maintenant, ProchaineTentative = maintenant };

    public static EvenementBot GameStarted(CustomGame game, DateTime maintenant) =>
        new() { Type = TypeEvenementBot.GameStarted, Game = game, OccurredAt = maintenant, ProchaineTentative = maintenant };

    public static EvenementBot GameCompleted(CustomGame game, DateTime maintenant) =>
        new() { Type = TypeEvenementBot.GameCompleted, Game = game, OccurredAt = maintenant, ProchaineTentative = maintenant };

    public void MarquerLivre(DateTime maintenant)
    {
        Tentatives++;
        Statut = StatutEvenementBot.Livre;
        LivreLe = maintenant;
        DerniereErreur = null;
    }

    /// <summary>
    /// Échec à retenter : attente de 5 s, doublée à chaque échec, plafonnée à 5 min. Passé
    /// <see cref="DureeDeVie"/>, l'événement est abandonné.
    /// </summary>
    public void Reporter(DateTime maintenant, string? raison)
    {
        Tentatives++;
        DerniereErreur = raison;
        if (maintenant - OccurredAt >= DureeDeVie)
        {
            Statut = StatutEvenementBot.Abandonne;
            return;
        }
        var attente = TimeSpan.FromSeconds(Math.Min(5 * Math.Pow(2, Math.Min(Tentatives - 1, 10)), 300));
        ProchaineTentative = maintenant + attente;
    }

    public void Abandonner(string? raison)
    {
        Tentatives++;
        Statut = StatutEvenementBot.Abandonne;
        DerniereErreur = raison;
    }
}

public enum TypeEvenementBot
{
    TeamsFormed,
    GameStarted,
    GameCompleted
}

public enum StatutEvenementBot
{
    ALivrer,
    Livre,
    Abandonne
}
