using Shameless.Application.DTOs.Match;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>
/// Les commandes d'une Série : la créer dans la Soirée ouverte, lancer sa prochaine game avec le
/// Side de chaque Équipe, saisir le résultat d'une game jusqu'à l'issue de la Série.
/// </summary>
public sealed class SeriesService
{
    public const int PlayersPerTeam = 5;

    private readonly IBestOfSeriesRepository _series;
    private readonly ICustomGameRepository _games;
    private readonly ISoireeRepository _soirees;
    private readonly IPlayerRepository _players;
    private readonly CostCalculationService _costs;
    private readonly TimeProvider _clock;

    public SeriesService(
        IBestOfSeriesRepository series,
        ICustomGameRepository games,
        ISoireeRepository soirees,
        IPlayerRepository players,
        CostCalculationService costs,
        TimeProvider clock)
    {
        _series = series;
        _games = games;
        _soirees = soirees;
        _players = players;
        _costs = costs;
        _clock = clock;
    }

    /// <summary>
    /// Crée une Série en cours dans la Soirée ouverte, entre deux Équipes de cinq joueurs distincts.
    /// Sert aussi au Picker (validation du tirage) et à la Revanche.
    /// </summary>
    public async Task<CommandResult<BestOfSeries>> CreateAsync(
        IReadOnlyCollection<int> teamA,
        IReadOnlyCollection<int> teamB,
        int format,
        bool fearless,
        bool alternateSides)
    {
        if (!BestOfSeries.IsValidFormat(format))
            return CommandResult<BestOfSeries>.Invalid("Le format d'une Série est BO1, BO2, BO3 ou BO5.");
        if (teamA.Count != PlayersPerTeam || teamB.Count != PlayersPerTeam)
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
            AlternateSides = alternateSides,
            Outcome = SeriesOutcome.EnCours,
            SoireeId = soiree.Id,
            Players = teamA.Select(id => new SeriesPlayer { PlayerId = id, Equipe = Equipes.A })
                .Concat(teamB.Select(id => new SeriesPlayer { PlayerId = id, Equipe = Equipes.B }))
                .ToList()
        };
        return CommandResult<BestOfSeries>.Ok(await _series.AddAsync(series));
    }

    /// <summary>
    /// Lance la prochaine game d'une Série en cours, avec sa composition courante. Le Side de
    /// l'Équipe A est celui demandé ; à défaut, avec l'alternance automatique, l'inverse de la
    /// game précédente (blue pour la première).
    /// </summary>
    public async Task<CommandResult<CustomGame>> PlayNextGameAsync(int seriesId, string? teamASide)
    {
        var series = await _series.GetWithGamesAsync(seriesId);
        if (series == null)
            return CommandResult<CustomGame>.NotFound("Cette Série n'existe pas.");
        if (!series.IsEnCours)
            return CommandResult<CustomGame>.Conflict("Cette Série est terminée : elle n'accepte plus de game.");
        if (series.Games.Any(g => g.Status == "pending"))
            return CommandResult<CustomGame>.Conflict("Une game de cette Série attend déjà son résultat.");

        if (teamASide != null && teamASide is not (Sides.Blue or Sides.Red))
            return CommandResult<CustomGame>.Invalid("Le Side d'une Équipe est « blue » ou « red ».");

        var previous = series.Games.LastOrDefault();
        var side = teamASide ?? (series.AlternateSides
            ? previous == null ? Sides.Blue : Sides.Other(previous.TeamASide)
            : null);
        if (side == null)
            return CommandResult<CustomGame>.Invalid("Choisissez le Side de l'Équipe A, ou activez l'alternance automatique.");

        var now = _clock.GetUtcNow().UtcDateTime;
        var game = new CustomGame
        {
            PlayedAt = now,
            CreatedAt = now,
            SeriesId = series.Id,
            TeamASide = side,
            Status = "pending",
            Participants = series.Players
                .Select(p => new CustomGameParticipant
                {
                    PlayerId = p.PlayerId,
                    Team = p.Equipe == Equipes.A ? side : Sides.Other(side)
                })
                .ToList()
        };
        await _games.AddAsync(game);
        return CommandResult<CustomGame>.Ok((await _games.GetByIdWithParticipantsAsync(game.Id))!);
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
        if (game.Status == "completed")
            return CommandResult<CustomGame>.Conflict("Le résultat de cette game est déjà saisi.");

        var series = game.SeriesId == null ? null : await _series.GetWithGamesAsync(game.SeriesId.Value);
        if (series is { IsEnCours: false })
            return CommandResult<CustomGame>.Conflict("Cette Série est terminée : son score ne change plus.");

        game.WinningTeam = winningTeam;
        game.Status = "completed";
        series?.ApplyResults(series.Games);
        await _games.SaveChangesAsync();

        await _costs.RecalculateCosts(gameId);

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

        var pending = series.Games.Where(g => g.Status != "completed").ToList();
        foreach (var game in pending)
            _games.Remove(game);

        var arret = series.Games.Count == pending.Count ? ArretSerie.Annulee : ArretSerie.Interrompue;
        if (arret == ArretSerie.Annulee)
            _series.Remove(series);
        else
            series.Outcome = SeriesOutcome.Interrompue;

        await _series.SaveChangesAsync();
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
        if (series.Games.Any(g => g.Status == "pending"))
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

        replaced.Player = replacement;
        replaced.PlayerId = replacement.Id;
        await _series.SaveChangesAsync();

        return CommandResult<SeriesResponse>.Ok(ToResponse(series));
    }

    public async Task<SeriesResponse?> GetAsync(int seriesId)
    {
        var series = await _series.GetWithGamesAsync(seriesId);
        return series == null ? null : ToResponse(series);
    }

    /// <summary>Les Séries en cours, de la plus ancienne à la plus récente.</summary>
    public async Task<List<SeriesResponse>> GetEnCoursAsync()
    {
        var result = new List<SeriesResponse>();
        foreach (var id in await _series.GetEnCoursIdsAsync())
            if (await GetAsync(id) is { } series)
                result.Add(series);
        return result;
    }

    private static SeriesResponse ToResponse(BestOfSeries series) => new(
        SeriesId: series.Id,
        SoireeId: series.SoireeId,
        Format: series.Format,
        Fearless: series.Fearless,
        AlternateSides: series.AlternateSides,
        Outcome: series.Outcome,
        WinningTeam: series.WinningTeam,
        TeamAWins: series.TeamAWins,
        TeamBWins: series.TeamBWins,
        TeamA: series.PlayersOf(Equipes.A).Select(p => GameParticipantDto.From(p.Player)).ToList(),
        TeamB: series.PlayersOf(Equipes.B).Select(p => GameParticipantDto.From(p.Player)).ToList(),
        Games: series.Games.Select(SeriesGameResponse.From).ToList());
}
