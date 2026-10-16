using Shameless.Application.Companion;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>
/// Les rapports de fin de game envoyés par le Companion avec le code de sa Série (ADR 0004). Un rapport
/// conforme à la composition de la game en attente la termine comme une saisie manuelle, qui reste
/// disponible en secours.
/// </summary>
public sealed class RapportsCompanionService
{
    private readonly IBestOfSeriesRepository _series;
    private readonly SeriesService _seriesService;

    public RapportsCompanionService(IBestOfSeriesRepository series, SeriesService seriesService)
    {
        _series = series;
        _seriesService = seriesService;
    }

    /// <summary>
    /// Rattache un rapport à la game en attente de la Série du code : si les joueurs détectés sont les
    /// participants de cette game (Remplacements compris), chacun sur son Side, la game est terminée
    /// avec le vainqueur du rapport et garde ses données Riot et les champions joués.
    /// </summary>
    public async Task<CommandResult<CustomGame>> RecevoirAsync(int seriesId, RapportDeGame rapport)
    {
        var invalidite = rapport.Invalidite();
        if (invalidite != null)
            return CommandResult<CustomGame>.Invalid(invalidite);

        var series = await _series.GetWithGamesAsync(seriesId);
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
        return await _seriesService.TerminerAsync(game, series, game.EquipeOn(rapport.SideGagnant));
    }
}
