using Shameless.Application.Bot;
using Shameless.Application.ChampionDraft;
using Shameless.Application.DTOs.Match;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Application.Picker;

namespace Shameless.Application.Services;

/// <summary>
/// Les commandes d'une Série : la créer dans la Soirée ouverte, lancer sa prochaine game (Side
/// automatique), saisir le résultat d'une game jusqu'à l'issue de la Série.
/// </summary>
public sealed class SeriesService
{
    private const string ModifieeEntreTemps =
        "Cette Série vient d'être modifiée par une autre commande : rechargez-la et réessayez.";

    private readonly IBestOfSeriesRepository _series;
    private readonly ICustomGameRepository _games;
    private readonly ISoireeRepository _soirees;
    private readonly IPlayerRepository _players;
    private readonly CostCalculationService _costs;
    private readonly TimeProvider _clock;
    private readonly IRandomSource _random;
    private readonly EvenementsBot _evenements;

    public SeriesService(
        IBestOfSeriesRepository series,
        ICustomGameRepository games,
        ISoireeRepository soirees,
        IPlayerRepository players,
        CostCalculationService costs,
        TimeProvider clock,
        IRandomSource random,
        EvenementsBot evenements)
    {
        _random = random;
        _evenements = evenements;
        _series = series;
        _games = games;
        _soirees = soirees;
        _players = players;
        _costs = costs;
        _clock = clock;
    }

    /// <summary>
    /// Crée une Série en cours dans la Soirée ouverte, entre deux Équipes de cinq joueurs distincts.
    /// Sert aussi à la Revanche.
    /// </summary>
    public async Task<CommandResult<BestOfSeries>> CreateAsync(
        IReadOnlyCollection<int> teamA,
        IReadOnlyCollection<int> teamB,
        int format,
        bool fearless)
    {
        var prepared = await PrepareAsync(teamA, teamB, format, fearless);
        if (!prepared.IsOk) return prepared;

        return await _series.TrySaveChangesAsync()
            ? prepared
            : CommandResult<BestOfSeries>.Conflict("La Série n'a pas pu être créée : réessayez.");
    }

    /// <summary>
    /// Prépare la création d'une Série (voir <see cref="CreateAsync"/>) sans l'enregistrer : l'appelant
    /// l'enregistre avec ses propres changements, en une transaction (validation du Picker).
    /// </summary>
    public async Task<CommandResult<BestOfSeries>> PrepareAsync(
        IReadOnlyCollection<int> teamA,
        IReadOnlyCollection<int> teamB,
        int format,
        bool fearless,
        IReadOnlyDictionary<int, Role>? roles = null,
        Capitaines? capitaines = null)
    {
        if (!BestOfSeries.IsValidFormat(format))
            return CommandResult<BestOfSeries>.Invalid("Le format d'une Série est BO1, BO2, BO3 ou BO5.");
        if (teamA.Count != Equipes.PlayersPerTeam || teamB.Count != Equipes.PlayersPerTeam)
            return CommandResult<BestOfSeries>.Invalid("Chaque Équipe compte exactement cinq joueurs.");

        var ids = teamA.Concat(teamB).ToList();
        if (ids.Distinct().Count() != ids.Count)
            return CommandResult<BestOfSeries>.Invalid("Les dix joueurs d'une Série sont tous distincts.");
        if ((await _players.GetByIdsAsync(ids)).Count != ids.Count)
            return CommandResult<BestOfSeries>.Invalid("Un des joueurs n'existe pas.");

        var soiree = await _soirees.GetOpenAsync();
        if (soiree == null)
            return CommandResult<BestOfSeries>.Conflict("Aucune Soirée n'est ouverte : ouvrez-en une pour créer une Série.");

        var series = new BestOfSeries
        {
            PlayedAt = _clock.GetUtcNow().UtcDateTime,
            Format = format,
            Fearless = fearless,
            Outcome = SeriesOutcome.EnCours,
            SoireeId = soiree.Id,
            Players = teamA.Select(id => NewPlayer(id, Equipes.A, capitaines?.CapitaineA))
                .Concat(teamB.Select(id => NewPlayer(id, Equipes.B, capitaines?.CapitaineB)))
                .ToList(),
            Liens = [LienAcces.Capitaine(Equipes.A), LienAcces.Capitaine(Equipes.B), LienAcces.Spectateur()]
        };
        _series.Add(series);
        _evenements.TeamsFormed(series);
        _evenements.LiensEnvoyes(series);
        return CommandResult<BestOfSeries>.Ok(series);

        SeriesPlayer NewPlayer(int playerId, string equipe, int? capitaine) => new()
        {
            PlayerId = playerId,
            Equipe = equipe,
            Role = roles != null && roles.TryGetValue(playerId, out var role) ? role : null,
            Capitaine = playerId == capitaine
        };
    }

    /// <summary>
    /// Lance la prochaine game d'une Série en cours, avec sa composition courante. Le Side n'est pas
    /// choisi : celui de l'Équipe A est tiré à pile ou face pour la première game, puis l'inverse de
    /// celui de la game précédente (corrigé par son Rapport le cas échéant).
    /// </summary>
    public async Task<CommandResult<CustomGame>> PlayNextGameAsync(int seriesId)
    {
        var series = await _series.GetWithGamesAsync(seriesId);
        if (series == null)
            return CommandResult<CustomGame>.NotFound("Cette Série n'existe pas.");
        if (!series.IsEnCours)
            return CommandResult<CustomGame>.Conflict("Cette Série est terminée : elle n'accepte plus de game.");
        if (series.Games.Any(g => g.Status == GameStatuses.Pending))
            return CommandResult<CustomGame>.Conflict("Une game de cette Série attend déjà son résultat.");

        if (series.CapitaineOf(Equipes.A) == null || series.CapitaineOf(Equipes.B) == null)
            return CommandResult<CustomGame>.Conflict("Désigne les deux capitaines avant de lancer la draft.");
        if (!series.CapitainesOntRejoint)
            return CommandResult<CustomGame>.Conflict("Les deux Capitaines doivent avoir rejoint avant de lancer la draft.");

        var game = NouvelleGame(series);
        series.Touch();
        if (!await _series.TrySaveChangesAsync())
            return CommandResult<CustomGame>.Conflict(ModifieeEntreTemps);

        return CommandResult<CustomGame>.Ok((await _games.GetByIdWithParticipantsAsync(game.Id))!);
    }

    /// <summary>
    /// La prochaine game de la Série, avec sa composition courante et son Side automatique : celui de
    /// l'Équipe A est tiré à pile ou face pour la première game, puis l'inverse de celui de la précédente.
    /// Ajoutée au contexte, non enregistrée.
    /// </summary>
    private CustomGame NouvelleGame(BestOfSeries series)
    {
        var side = Sides.ProchainDeLEquipeA(series.Games.LastOrDefault()?.TeamASide, _random);

        var now = _clock.GetUtcNow().UtcDateTime;
        var game = new CustomGame
        {
            PlayedAt = now,
            CreatedAt = now,
            SeriesId = series.Id,
            TeamASide = side,
            Status = GameStatuses.Pending,
            // Triés : l'ordre des participants (leur Id) est l'ordre de composition de l'Équipe, et la base
            // ne garantit aucun ordre sans tri.
            Participants = series.Players
                .OrderBy(p => p.Id)
                .Select(p => new CustomGameParticipant
                {
                    PlayerId = p.PlayerId,
                    Side = p.Equipe == Equipes.A ? side : Sides.Other(side)
                })
                .ToList()
        };
        _games.Add(game);
        return game;
    }

    /// <summary>
    /// Un Capitaine se met « prêt » pour la game à venir, ou se remet « pas prêt » (le Lien capitaine fait
    /// foi de son Équipe). Il doit avoir rejoint et être le Capitaine de son Équipe. Quand les deux
    /// Capitaines sont prêts, la Game est créée et sa Draft de champions ouverte dans la même
    /// transaction : aucun autre clic. Refusé une fois la game créée.
    /// </summary>
    public async Task<CommandResult<EtatDuPret>> DefinirPretAsync(string secret, bool pret)
    {
        var lien = await _series.GetLienAsync(secret);
        if (lien is not { Portee: PorteeLien.Capitaine, Equipe: { } equipe })
            return CommandResult<EtatDuPret>.NotFound("Ce lien n'est pas un Lien capitaine.");

        var series = await _series.GetWithGamesAsync(lien.SeriesId);
        if (series == null)
            return CommandResult<EtatDuPret>.NotFound("Cette Série n'existe pas.");
        if (!series.IsEnCours)
            return CommandResult<EtatDuPret>.Conflict("Cette Série est terminée : plus personne n'a à être prêt.");
        if (series.Games.Any(g => g.Status == GameStatuses.Pending && g.DraftChampions != null))
            return CommandResult<EtatDuPret>.Conflict("La draft a déjà démarré : on ne change plus d'avis.");

        lien = series.LienCapitaine(equipe);
        if (pret)
        {
            if (lien.RejointLe == null)
                return CommandResult<EtatDuPret>.Conflict("Ouvre ton Lien capitaine pour rejoindre la Série avant d'être prêt.");
            if (series.CapitaineOf(equipe) == null)
                return CommandResult<EtatDuPret>.Conflict("Ton Équipe n'a pas de Capitaine : l'organisateur le désigne dans le Picker.");
        }

        lien.DefinirPret(pret, _clock.GetUtcNow().UtcDateTime);
        if (series.CapitainesSontPrets)
        {
            var maintenant = _clock.GetUtcNow().UtcDateTime;
            // Les deux Capitaines sont là : la Game et sa Draft de champions démarrent ensemble.
            // Après un Reset accepté, la Game est restée : on lui rouvre sa draft au lieu d'en créer une autre.
            var game = series.Games.LastOrDefault(g => g.Status == GameStatuses.Pending) ?? NouvelleGame(series);
            game.DraftChampions = new DraftChampions
            {
                TourCommenceLe = maintenant.AddTicks(-(maintenant.Ticks % 10)) // microseconde, comme PostgreSQL
            };
        }

        series.Touch();
        if (!await _series.TrySaveChangesAsync())
            return CommandResult<EtatDuPret>.Conflict(ModifieeEntreTemps);

        return CommandResult<EtatDuPret>.Ok(EtatDuPret.De(series, equipe));
    }

    /// <summary>
    /// Le Début de la Game (ADR 0017) : le signal « partie lancée » du Companion, ou de l'organisateur à
    /// la main. Il renseigne <c>StartedAt</c>, clôt les pronostics et émet <c>game_started</c>. Idempotent :
    /// un second signal répond le même début sans rien ré-émettre. Refusé sans game en attente.
    /// </summary>
    public async Task<CommandResult<CustomGame>> SignalerDebutAsync(int seriesId)
    {
        var series = await _series.GetWithGamesAsync(seriesId);
        if (series == null)
            return CommandResult<CustomGame>.NotFound("Cette Série n'existe pas.");
        if (!series.IsEnCours)
            return CommandResult<CustomGame>.Conflict("Cette Série est terminée : aucune partie ne peut commencer.");

        var game = series.Games.SingleOrDefault(g => g.Status == GameStatuses.Pending);
        if (game == null)
            return CommandResult<CustomGame>.Conflict(
                "Aucune game de cette Série n'est en attente : lancez la draft avant de signaler le début de la partie.");
        if (game.StartedAt != null)
            return CommandResult<CustomGame>.Ok(game);

        // Arrondi à la microseconde, la précision de PostgreSQL : le début répondu au premier signal est
        // celui que relira le second.
        var maintenant = _clock.GetUtcNow().UtcDateTime;
        game.StartedAt = maintenant.AddTicks(-(maintenant.Ticks % 10));
        // Le début de la Game : pronostics clos, fenêtre de présence du bot ouverte.
        _evenements.GameStarted(game);
        if (!await _series.TrySaveChangesAsync())
            return CommandResult<CustomGame>.Conflict(ModifieeEntreTemps);

        return CommandResult<CustomGame>.Ok(game);
    }

    /// <summary>
    /// Saisit l'Équipe gagnante d'une game en attente, met à jour le score et l'issue de sa Série,
    /// puis recalcule le cost des joueurs.
    /// </summary>
    public async Task<CommandResult<CustomGame>> RecordResultAsync(int gameId, string winningTeam)
    {
        if (!Equipes.IsValid(winningTeam))
            return CommandResult<CustomGame>.Invalid("Le vainqueur d'une game est une Équipe : « A » ou « B ».");

        var game = await _games.GetByIdWithParticipantsAsync(gameId);
        if (game == null)
            return CommandResult<CustomGame>.NotFound("Cette game n'existe pas.");
        if (game.Status == GameStatuses.Completed)
            return CommandResult<CustomGame>.Conflict("Le résultat de cette game est déjà saisi.");

        var series = game.SeriesId == null ? null : await _series.GetWithGamesAsync(game.SeriesId.Value);
        if (series is { IsEnCours: false })
            return CommandResult<CustomGame>.Conflict("Cette Série est terminée : son score ne change plus.");
        if (game.RapportEnRevue != null)
            return CommandResult<CustomGame>.Conflict(
                "Un rapport du Companion de cette game est en revue : acceptez-le ou rejetez-le avant de saisir le résultat.");

        return await TerminerAsync(game, series, winningTeam);
    }

    /// <summary>
    /// Termine une game en attente avec son Équipe gagnante, que le résultat vienne d'une saisie
    /// manuelle ou d'un rapport du Companion : score et issue de la Série recalculés, puis cost des
    /// joueurs. Les vérifications (game en attente, Série en cours) sont faites par l'appelant.
    /// </summary>
    public async Task<CommandResult<CustomGame>> TerminerAsync(CustomGame game, BestOfSeries? series, string winningTeam)
    {
        game.WinningTeam = winningTeam;
        game.Status = GameStatuses.Completed;
        var maintenant = _clock.GetUtcNow().UtcDateTime;
        game.CompletedAt = maintenant.AddTicks(-(maintenant.Ticks % 10)); // microseconde, comme PostgreSQL
        if (game.StartedAt == null)
        {
            // Résultat sans signal de début : la partie a forcément commencé. Le bot reçoit toujours les deux
            // événements, game_started d'abord ; un début déjà signalé n'est jamais écrasé ni ré-émis.
            game.StartedAt = game.CompletedAt;
            _evenements.GameStarted(game);
        }
        series?.ApplyResults(series.Games);
        series?.RemettrePasPrets();
        series?.Touch();
        _evenements.GameCompleted(game);
        if (!await _games.TrySaveChangesAsync())
            return CommandResult<CustomGame>.Conflict("Le résultat de cette game vient d'être saisi par une autre commande.");

        await _costs.RecalculateCosts(game.Id);

        return CommandResult<CustomGame>.Ok(game);
    }

    /// <summary>
    /// Arrête une Série en cours (ADR 0009). Sans game terminée, elle est annulée : effacée avec ses
    /// games en attente. Sinon elle est interrompue : elle se termine sans vainqueur, ses games en
    /// attente sont supprimées et ses games terminées restent et comptent. Aucun cost n'est recalculé :
    /// aucune game terminée n'est effacée.
    /// </summary>
    public async Task<CommandResult<ArretSerie>> StopAsync(int seriesId)
    {
        var series = await _series.GetWithGamesAsync(seriesId);
        if (series == null)
            return CommandResult<ArretSerie>.NotFound("Cette Série n'existe pas.");
        if (!series.IsEnCours)
            return CommandResult<ArretSerie>.Conflict("Cette Série est terminée : elle ne peut plus être arrêtée.");

        var pending = series.Games.Where(g => g.Status != GameStatuses.Completed).ToList();
        foreach (var game in pending)
            _games.Remove(game);

        var arret = series.Games.Count == pending.Count ? ArretSerie.Annulee : ArretSerie.Interrompue;
        if (arret == ArretSerie.Annulee)
            _series.Remove(series);
        else
            series.Outcome = SeriesOutcome.Interrompue;

        series.Touch();
        if (!await _series.TrySaveChangesAsync())
            return CommandResult<ArretSerie>.Conflict(ModifieeEntreTemps);
        return CommandResult<ArretSerie>.Ok(arret);
    }

    /// <summary>
    /// Remplace, entre deux games d'une Série en cours, un joueur d'une Équipe par un joueur qui
    /// n'est pas dans la Série. Seule la composition courante change : elle s'applique à la
    /// prochaine game, les games déjà lancées gardent leurs participants et le score ne bouge pas.
    /// Refusé tant qu'une game attend son résultat : on n'est alors pas « entre deux games ».
    /// </summary>
    public async Task<CommandResult<SeriesResponse>> ReplacePlayerAsync(int seriesId, int replacedPlayerId, int replacementPlayerId)
    {
        var series = await _series.GetWithGamesAsync(seriesId);
        if (series == null)
            return CommandResult<SeriesResponse>.NotFound("Cette Série n'existe pas.");
        if (!series.IsEnCours)
            return CommandResult<SeriesResponse>.Conflict("Cette Série est terminée : sa composition ne change plus.");
        if (series.Games.Any(g => g.Status == GameStatuses.Pending))
            return CommandResult<SeriesResponse>.Conflict(
                "Une game de cette Série attend son résultat : le remplacement se fait entre deux games.");

        var replaced = series.Players.SingleOrDefault(p => p.PlayerId == replacedPlayerId);
        if (replaced == null)
            return CommandResult<SeriesResponse>.Invalid("Le joueur remplacé n'est pas dans cette Série.");
        if (series.Players.Any(p => p.PlayerId == replacementPlayerId))
            return CommandResult<SeriesResponse>.Conflict("Le remplaçant est déjà dans cette Série.");
        var replacement = await _players.GetByIdAsync(replacementPlayerId);
        if (replacement == null)
            return CommandResult<SeriesResponse>.Invalid("Le remplaçant n'existe pas.");

        series.Remplacer(replaced, replacement);
        series.Touch();
        if (!await _series.TrySaveChangesAsync())
            return CommandResult<SeriesResponse>.Conflict(ModifieeEntreTemps);

        return CommandResult<SeriesResponse>.Ok(await ToResponseAsync(series));
    }

    /// <summary>
    /// Désigne le Capitaine d'une Équipe d'une Série en cours, parmi sa composition courante : le joueur
    /// choisi (<see cref="DesignationCapitaine.Manuel"/>), un joueur au hasard, ou le plus gros cost de
    /// l'Équipe (à cost égal, l'ordre de la composition départage). Remplace le capitaine précédent.
    /// </summary>
    public async Task<CommandResult<SeriesResponse>> DesignerCapitaineAsync(
        int seriesId, string equipe, DesignationCapitaine designation, int? playerId)
    {
        if (!Equipes.IsValid(equipe))
            return CommandResult<SeriesResponse>.Invalid("Le capitaine est désigné pour l'Équipe A ou l'Équipe B.");

        var series = await _series.GetWithGamesAsync(seriesId);
        if (series == null)
            return CommandResult<SeriesResponse>.NotFound("Cette Série n'existe pas.");
        if (!series.IsEnCours)
            return CommandResult<SeriesResponse>.Conflict("Cette Série est terminée : ses capitaines ne changent plus.");

        if (!Enum.IsDefined(designation))
            return CommandResult<SeriesResponse>.Invalid("Désignation du capitaine inconnue.");

        var joueurs = series.PlayersOf(equipe).OrderBy(p => p.Id).ToList();
        var designe = DesignationDesCapitaines.Designer(
            designation, joueurs.Select(p => new PickerPlayer(p.PlayerId, p.Player.CurrentCost)).ToList(), playerId, _random);
        if (designe == null)
            return CommandResult<SeriesResponse>.Invalid($"Le capitaine fait partie de l'Équipe {equipe}.");

        series.DesignerCapitaine(joueurs.Single(p => p.PlayerId == designe));
        series.Touch();
        if (!await _series.TrySaveChangesAsync())
            return CommandResult<SeriesResponse>.Conflict(ModifieeEntreTemps);

        return CommandResult<SeriesResponse>.Ok(await ToResponseAsync(series));
    }

    /// <summary>
    /// Lance la Revanche d'une Série : une nouvelle Série dans la même Soirée, entre les mêmes
    /// Équipes dans leur dernière composition (Remplacements compris), sans repasser par le Picker.
    /// Le format et l'option Fearless sont choisis pour la Revanche.
    /// </summary>
    public async Task<CommandResult<BestOfSeries>> CreateRevancheAsync(
        int seriesId,
        int format,
        bool fearless)
    {
        var series = await _series.GetWithGamesAsync(seriesId);
        if (series == null)
            return CommandResult<BestOfSeries>.NotFound("Cette Série n'existe pas.");
        if (series.IsEnCours)
            return CommandResult<BestOfSeries>.Conflict("Cette Série est en cours : la Revanche se lance une fois la Série terminée.");

        var soiree = await _soirees.GetOpenAsync();
        if (soiree == null || soiree.Id != series.SoireeId)
            return CommandResult<BestOfSeries>.Conflict("La Soirée de cette Série est fermée : la Revanche n'est plus possible.");

        return await CreateAsync(
            series.PlayersOf(Equipes.A).Select(p => p.PlayerId).ToList(),
            series.PlayersOf(Equipes.B).Select(p => p.PlayerId).ToList(),
            format,
            fearless);
    }

    public async Task<SeriesResponse?> GetAsync(int seriesId)
    {
        var series = await _series.GetWithGamesAsync(seriesId);
        return series == null ? null : await ToResponseAsync(series);
    }

    /// <summary>Les Séries en cours, de la plus ancienne à la plus récente.</summary>
    public async Task<List<SeriesResponse>> GetEnCoursAsync() =>
        await ToResponsesAsync(await _series.GetEnCoursWithGamesAsync());

    /// <summary>Les Séries d'une Soirée avec leur issue, de la plus ancienne à la plus récente.</summary>
    public async Task<List<SeriesResponse>> GetOfSoireeAsync(int soireeId) =>
        await ToResponsesAsync(await _series.GetBySoireeWithGamesAsync(soireeId));

    private async Task<SeriesResponse> ToResponseAsync(BestOfSeries series) =>
        (await ToResponsesAsync([series])).Single();

    /// <summary>
    /// Les Séries lues ; les joueurs détectés inattendus de leurs rapports en revue sont reconnus par
    /// leur PUUID quand ils sont enregistrés dans Shameless.
    /// </summary>
    private async Task<List<SeriesResponse>> ToResponsesAsync(IReadOnlyCollection<BestOfSeries> series)
    {
        var puuids = series.SelectMany(s => s.Games)
            .Select(g => g.RapportEnRevue)
            .OfType<RapportCompanion>()
            .SelectMany(r => r.Rapport().Joueurs.Select(j => j.Puuid))
            .Distinct()
            .ToList();
        var joueursConnus = puuids.Count == 0
            ? new Dictionary<string, Player>()
            : (await _players.GetByPuuidsAsync(puuids)).GroupBy(p => p.PUUID).ToDictionary(g => g.Key, g => g.First());
        return series.Select(s => ToResponse(s, joueursConnus)).ToList();
    }

    /// <summary>La Série lue ; les rôles de l'Autorole n'y figurent pas pendant une Draft de champions.</summary>
    private static SeriesResponse ToResponse(BestOfSeries series, IReadOnlyDictionary<string, Player> joueursConnus) => new(
        SeriesId: series.Id,
        SoireeId: series.SoireeId,
        Format: series.Format,
        Fearless: series.Fearless,
        Outcome: series.Outcome,
        WinningTeam: series.WinningTeam,
        TeamAWins: series.TeamAWins,
        TeamBWins: series.TeamBWins,
        TeamA: series.PlayersOf(Equipes.A).Select(p => GameParticipantDto.From(p.Player, series.RolesCaches ? null : p.Role)).ToList(),
        TeamB: series.PlayersOf(Equipes.B).Select(p => GameParticipantDto.From(p.Player, series.RolesCaches ? null : p.Role)).ToList(),
        CapitaineA: series.CapitaineOf(Equipes.A),
        CapitaineB: series.CapitaineOf(Equipes.B),
        Games: series.Games.Select(g => SeriesGameResponse.From(g, joueursConnus)).ToList());
}
