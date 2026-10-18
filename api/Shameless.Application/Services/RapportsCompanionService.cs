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
/// rejette. Un identifiant Riot n'est rattaché qu'à une game, garanti par la base : le même rapport
/// renvoyé (reprise réseau, plusieurs joueurs équipés) désigne la game déjà rattachée, ou le rapport
/// déjà en revue ou rejeté, sans rien créer.
/// </summary>
public sealed class RapportsCompanionService
{
    private readonly IBestOfSeriesRepository _series;
    private readonly ICustomGameRepository _games;
    private readonly SeriesService _seriesService;
    private readonly IPlayerRepository _players;
    private readonly IRiotService _riot;
    private readonly TimeProvider _clock;

    public RapportsCompanionService(
        IBestOfSeriesRepository series,
        ICustomGameRepository games,
        SeriesService seriesService,
        IPlayerRepository players,
        IRiotService riot,
        TimeProvider clock)
    {
        _series = series;
        _games = games;
        _seriesService = seriesService;
        _players = players;
        _riot = riot;
        _clock = clock;
    }

    /// <summary>
    /// Rattache un rapport à la game en attente de la Série du code : si les joueurs détectés sont les
    /// participants de cette game (Remplacements compris), chacun sur son Side, la game est terminée
    /// avec le vainqueur du rapport et garde ses données Riot et les champions joués. Sinon le rapport
    /// est en revue et la game reste en attente. Un identifiant Riot déjà reçu désigne sa game, sans
    /// rien modifier, avant toute autre vérification. Tant qu'un rapport est en revue, un autre rapport
    /// de la game est refusé ; le même (même identifiant Riot) répond son statut, « en revue » ou « rejeté ».
    /// </summary>
    public async Task<CommandResult<RapportTraite>> RecevoirAsync(int seriesId, RapportDeGame rapport)
    {
        var invalidite = rapport.Invalidite();
        if (invalidite != null)
            return CommandResult<RapportTraite>.Invalid(invalidite);

        // La Série est lue avant de chercher l'identifiant Riot : un envoi simultané qui rattache le
        // même rapport entre les deux lectures est vu comme déjà reçu, jamais comme une game manquante.
        var series = await _series.GetWithGamesAsync(seriesId);
        var dejaRecu = await DejaRecuAsync(seriesId, rapport);
        if (dejaRecu != null)
            return dejaRecu;

        if (series is not { IsEnCours: true })
            return CommandResult<RapportTraite>.NotFound("Cette Série n'existe pas ou est terminée.");

        var game = series.Games.SingleOrDefault(g => g.Status == GameStatuses.Pending);
        if (game == null)
            return CommandResult<RapportTraite>.Conflict(
                "Aucune game de cette Série n'attend de rapport : lancez la game avant d'envoyer son rapport.");

        // Le même rapport (même identifiant Riot) a déjà été reconnu ci-dessus : celui-ci en est un autre.
        if (game.RapportEnRevue != null)
            return CommandResult<RapportTraite>.Conflict(
                "Un rapport de la game en attente est déjà en revue : l'organisateur doit l'accepter ou le rejeter.");

        var ecart = rapport.EcartAvec(game);
        if (!ecart.EstVide)
        {
            rapport = await AvecRiotIdsDesInconnusAsync(rapport, ecart);
            game.Rapports.Add(RapportCompanion.EnRevue(rapport, Maintenant()));
            if (await _games.TrySaveChangesAsync())
                return EnRevue(game);

            // Refusé par la base : un envoi simultané a mis ce rapport, ou un autre, en revue entre-temps.
            return await DejaRecuAsync(seriesId, rapport) ?? CommandResult<RapportTraite>.Conflict(
                "Un autre rapport de cette game vient d'être mis en revue : l'organisateur doit l'accepter ou le rejeter.");
        }

        game.AppliquerRapport(rapport);
        var terminee = Accepte(await _seriesService.TerminerAsync(game, series, rapport.EquipeGagnanteDans(game)));
        if (terminee.IsOk)
            return terminee;

        // Refusé par la base : un envoi simultané du même rapport a pu le rattacher entre-temps.
        return await DejaRecuAsync(seriesId, rapport) ?? terminee;
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

    /// <summary>
    /// Le rapport où chaque joueur détecté inattendu, inconnu de l'Instance, porte son RiotId pour que
    /// l'organisateur sache qui c'est. Riot qui ne répond pas laisse le joueur sans RiotId : le rapport
    /// est mis en revue quand même, signalé par son PUUID.
    /// </summary>
    private async Task<RapportDeGame> AvecRiotIdsDesInconnusAsync(RapportDeGame rapport, EcartDeComposition ecart)
    {
        var inattendus = ecart.Inattendus.ToList();
        if (inattendus.Count == 0)
            return rapport;

        var connus = (await _players.GetByPuuidsAsync(inattendus)).Select(p => p.PUUID).ToHashSet();
        var riotIds = new Dictionary<string, string?>();
        foreach (var puuid in inattendus.Where(p => !connus.Contains(p)))
        {
            try { riotIds[puuid] = await _riot.GetRiotIdFromPuuidAsync(puuid); }
            catch (HttpRequestException) { riotIds[puuid] = null; }
        }
        return rapport with
        {
            Joueurs = rapport.Joueurs
                .Select(j => riotIds.TryGetValue(j.Puuid, out var riotId) ? j with { RiotId = riotId } : j)
                .ToList()
        };
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

    /// <summary>
    /// Le rapport déjà reçu sous l'identifiant Riot du rapport, sans rien modifier, ou null s'il ne
    /// l'a jamais été : la game qu'il a terminée (Accepte), ou sa game s'il est en revue ou rejeté.
    /// Reçu pour une game d'une autre Série : refusé, sans rien en dévoiler.
    /// </summary>
    private async Task<CommandResult<RapportTraite>?> DejaRecuAsync(int seriesId, RapportDeGame rapport)
    {
        var game = await _games.GetByRiotMatchIdAsync(rapport.RiotMatchId);
        var statut = StatutRapport.Accepte;
        if (game == null)
        {
            var garde = await _games.GetRapportByRiotMatchIdAsync(rapport.RiotMatchId);
            if (garde == null)
                return null;
            (game, statut) = (garde.Game, garde.Statut);
        }
        if (game.SeriesId != seriesId)
            return CommandResult<RapportTraite>.Conflict(
                "Cette partie Riot est déjà rattachée à une game d'une autre Série.");

        return CommandResult<RapportTraite>.Ok(new RapportTraite(game, statut));
    }

    private static CommandResult<RapportTraite> EnRevue(CustomGame game) =>
        CommandResult<RapportTraite>.Ok(new RapportTraite(game, StatutRapport.EnRevue));

    private static CommandResult<RapportTraite> Accepte(CommandResult<CustomGame> terminee) =>
        terminee.IsOk
            ? CommandResult<RapportTraite>.Ok(new RapportTraite(terminee.Value!, StatutRapport.Accepte))
            : terminee.As<RapportTraite>();

    private DateTime Maintenant() => _clock.GetUtcNow().UtcDateTime;
}
