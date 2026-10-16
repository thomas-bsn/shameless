using Shameless.Application.Companion;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>Un rapport traité : sa game et où en est le rapport.</summary>
public sealed record RapportTraite(CustomGame Game, StatutRapport Statut);

/// <summary>
/// Les rapports de fin de game envoyés par le Companion avec le code de sa Série (ADR 0004). Un rapport
/// conforme à la composition de la game en attente la termine comme une saisie manuelle, qui reste
/// disponible en secours. Un rapport qui s'en écarte est en revue : l'organisateur l'accepte ou le
/// rejette.
/// </summary>
public sealed class RapportsCompanionService
{
    private readonly IBestOfSeriesRepository _series;
    private readonly ICustomGameRepository _games;
    private readonly SeriesService _seriesService;
    private readonly TimeProvider _clock;

    public RapportsCompanionService(
        IBestOfSeriesRepository series,
        ICustomGameRepository games,
        SeriesService seriesService,
        TimeProvider clock)
    {
        _series = series;
        _games = games;
        _seriesService = seriesService;
        _clock = clock;
    }

    /// <summary>
    /// Rattache un rapport à la game en attente de la Série du code : si les joueurs détectés sont les
    /// participants de cette game (Remplacements compris), chacun sur son Side, la game est terminée
    /// avec le vainqueur du rapport et garde ses données Riot et les champions joués. Sinon le rapport
    /// est en revue et la game reste en attente. Tant qu'un rapport est en revue, un autre rapport de
    /// la game est refusé ; le même (même identifiant Riot) répond à nouveau « en revue ».
    /// </summary>
    public async Task<CommandResult<RapportTraite>> RecevoirAsync(int seriesId, RapportDeGame rapport)
    {
        var invalidite = rapport.Invalidite();
        if (invalidite != null)
            return CommandResult<RapportTraite>.Invalid(invalidite);

        var series = await _series.GetWithGamesAsync(seriesId);
        if (series is not { IsEnCours: true })
            return CommandResult<RapportTraite>.NotFound("Cette Série n'existe pas ou est terminée.");

        var game = series.Games.SingleOrDefault(g => g.Status == GameStatuses.Pending);
        if (game == null)
            return CommandResult<RapportTraite>.Conflict(
                "Aucune game de cette Série n'attend de rapport : lancez la game avant d'envoyer son rapport.");

        if (game.RapportEnRevue is { } revue)
            return revue.RiotMatchId == rapport.RiotMatchId
                ? CommandResult<RapportTraite>.Ok(new RapportTraite(game, StatutRapport.EnRevue))
                : CommandResult<RapportTraite>.Conflict(
                    "Un rapport de la game en attente est déjà en revue : l'organisateur doit l'accepter ou le rejeter.");

        if (!rapport.EcartAvec(game).EstVide)
        {
            game.Rapports.Add(RapportCompanion.EnRevue(rapport, Maintenant()));
            if (!await _games.TrySaveChangesAsync())
                return CommandResult<RapportTraite>.Conflict(
                    "Un autre rapport de cette game vient d'être reçu : réessayez.");
            return CommandResult<RapportTraite>.Ok(new RapportTraite(game, StatutRapport.EnRevue));
        }

        game.AppliquerRapport(rapport);
        return Accepte(await _seriesService.TerminerAsync(game, series, rapport.EquipeGagnanteDans(game)));
    }

    /// <summary>
    /// L'organisateur accepte un rapport en revue : la game est terminée avec ce rapport, comme pour un
    /// rapport conforme. L'Équipe gagnante est celle des participants détectés sur le Side gagnant.
    /// </summary>
    public async Task<CommandResult<RapportTraite>> AccepterAsync(int rapportId)
    {
        var enRevue = await EnRevueAsync(rapportId);
        if (!enRevue.IsOk) return enRevue.As<RapportTraite>();
        var (revue, game, series) = enRevue.Value!;

        var rapport = revue.Rapport();
        game.AppliquerRapport(rapport);
        revue.Decider(StatutRapport.Accepte, Maintenant());
        return Accepte(await _seriesService.TerminerAsync(game, series, rapport.EquipeGagnanteDans(game)));
    }

    /// <summary>
    /// L'organisateur rejette un rapport en revue : il est écarté et la game attend toujours un autre
    /// rapport ou une saisie manuelle.
    /// </summary>
    public async Task<CommandResult<RapportTraite>> RejeterAsync(int rapportId)
    {
        var enRevue = await EnRevueAsync(rapportId);
        if (!enRevue.IsOk) return enRevue.As<RapportTraite>();
        var (revue, game, _) = enRevue.Value!;

        revue.Decider(StatutRapport.Rejete, Maintenant());
        if (!await _games.TrySaveChangesAsync())
            return CommandResult<RapportTraite>.Conflict(DejaDecide);
        return CommandResult<RapportTraite>.Ok(new RapportTraite(game, StatutRapport.Rejete));
    }

    private const string DejaDecide = "Ce rapport n'est plus en revue : l'organisateur a déjà décidé.";

    private sealed record Revue(RapportCompanion Rapport, CustomGame Game, BestOfSeries Series);

    private async Task<CommandResult<Revue>> EnRevueAsync(int rapportId)
    {
        var revue = await _games.GetRapportAsync(rapportId);
        if (revue == null)
            return CommandResult<Revue>.NotFound("Ce rapport n'existe pas, ou a disparu avec sa game.");
        if (revue.Statut != StatutRapport.EnRevue)
            return CommandResult<Revue>.Conflict(DejaDecide);

        var series = revue.Game.SeriesId == null ? null : await _series.GetWithGamesAsync(revue.Game.SeriesId.Value);
        if (series is not { IsEnCours: true })
            return CommandResult<Revue>.Conflict("Cette Série est terminée : son score ne change plus.");
        return CommandResult<Revue>.Ok(new Revue(revue, series.Games.Single(g => g.Id == revue.GameId), series));
    }

    private static CommandResult<RapportTraite> Accepte(CommandResult<CustomGame> terminee) =>
        terminee.IsOk
            ? CommandResult<RapportTraite>.Ok(new RapportTraite(terminee.Value!, StatutRapport.Accepte))
            : terminee.As<RapportTraite>();

    private DateTime Maintenant() => _clock.GetUtcNow().UtcDateTime;
}
