using Shameless.Application.Companion;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>
/// Les rapports de fin de game envoyés par le Companion avec le code de sa Série (ADR 0004). Un rapport
/// conforme à la composition de la game en attente la termine comme une saisie manuelle, qui reste
/// disponible en secours. Un identifiant Riot n'est rattaché qu'à une game, garanti par la base : le
/// même rapport renvoyé (reprise réseau, plusieurs joueurs équipés) désigne la game déjà rattachée.
/// </summary>
public sealed class RapportsCompanionService
{
    private readonly IBestOfSeriesRepository _series;
    private readonly ICustomGameRepository _games;
    private readonly SeriesService _seriesService;

    public RapportsCompanionService(
        IBestOfSeriesRepository series,
        ICustomGameRepository games,
        SeriesService seriesService)
    {
        _series = series;
        _games = games;
        _seriesService = seriesService;
    }

    /// <summary>
    /// Rattache un rapport à la game en attente de la Série du code : si les joueurs détectés sont les
    /// participants de cette game (Remplacements compris), chacun sur son Side, la game est terminée
    /// avec le vainqueur du rapport et garde ses données Riot et les champions joués. Un identifiant
    /// Riot déjà reçu désigne sa game, sans rien modifier, avant toute autre vérification.
    /// </summary>
    public async Task<CommandResult<CustomGame>> RecevoirAsync(int seriesId, RapportDeGame rapport)
    {
        var invalidite = rapport.Invalidite();
        if (invalidite != null)
            return CommandResult<CustomGame>.Invalid(invalidite);

        // La Série est lue avant de chercher l'identifiant Riot : un envoi simultané qui rattache le
        // même rapport entre les deux lectures est vu comme déjà reçu, jamais comme une game manquante.
        var series = await _series.GetWithGamesAsync(seriesId);
        var dejaRecu = await DejaRecuAsync(seriesId, rapport);
        if (dejaRecu != null)
            return dejaRecu;

        if (series is not { IsEnCours: true })
            return CommandResult<CustomGame>.NotFound("Cette Série n'existe pas ou est terminée.");

        var game = series.Games.SingleOrDefault(g => g.Status == GameStatuses.Pending);
        if (game == null)
            return CommandResult<CustomGame>.Conflict(
                "Aucune game de cette Série n'attend de rapport : lancez la game avant d'envoyer son rapport.");

        var ecart = rapport.EcartAvec(game);
        if (!ecart.EstVide)
            return CommandResult<CustomGame>.Conflict(
                "Les joueurs du rapport ne correspondent pas à la composition de la game en attente.");

        game.AppliquerRapport(rapport);
        var terminee = await _seriesService.TerminerAsync(game, series, game.EquipeOn(rapport.SideGagnant));
        if (terminee.IsOk)
            return terminee;

        // Refusé par la base : un envoi simultané du même rapport a pu le rattacher entre-temps.
        return await DejaRecuAsync(seriesId, rapport) ?? terminee;
    }

    /// <summary>
    /// La game déjà rattachée à l'identifiant Riot du rapport, sans rien modifier, ou null s'il n'a
    /// jamais été reçu. Rattaché à une game d'une autre Série : refusé, sans rien en dévoiler.
    /// </summary>
    private async Task<CommandResult<CustomGame>?> DejaRecuAsync(int seriesId, RapportDeGame rapport)
    {
        var game = await _games.GetByRiotMatchIdAsync(rapport.RiotMatchId);
        if (game == null)
            return null;
        if (game.SeriesId != seriesId)
            return CommandResult<CustomGame>.Conflict(
                "Cette partie Riot est déjà rattachée à une game d'une autre Série.");

        return CommandResult<CustomGame>.Ok(game);
    }
}
