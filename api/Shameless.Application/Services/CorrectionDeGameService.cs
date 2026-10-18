using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>
/// La correction d'une game terminée par un Administrateur (ADR 0009) : son vainqueur, sa composition
/// ou sa suppression. La Série (score, issue) et le cost du mois des joueurs concernés sont recalculés ;
/// une Série qui n'a plus de game terminée est effacée, comme une Série annulée.
/// </summary>
public sealed class CorrectionDeGameService
{
    private const string ModifieeEntreTemps =
        "Cette game ou sa Série vient d'être modifiée par une autre commande : rechargez-la et réessayez.";

    private readonly ICustomGameRepository _games;
    private readonly IBestOfSeriesRepository _series;
    private readonly IPlayerRepository _players;
    private readonly CostCalculationService _costs;
    private readonly TimeProvider _clock;

    public CorrectionDeGameService(
        ICustomGameRepository games,
        IBestOfSeriesRepository series,
        IPlayerRepository players,
        CostCalculationService costs,
        TimeProvider clock)
    {
        _games = games;
        _series = series;
        _players = players;
        _costs = costs;
        _clock = clock;
    }

    /// <summary>
    /// Corrige le vainqueur et/ou la composition (les cinq joueurs de chaque Équipe) d'une game terminée.
    /// Sans changement réel, la game n'est pas marquée « modifiée par un admin ». Les stats du Companion
    /// restent celles des joueurs conservés ; un joueur ajouté n'en a pas.
    /// </summary>
    public async Task<CommandResult<CustomGame>> CorrigerAsync(
        int gameId, string? winner, IReadOnlyCollection<int>? teamA, IReadOnlyCollection<int>? teamB)
    {
        if (winner == null && teamA == null && teamB == null)
            return CommandResult<CustomGame>.Invalid("Indiquez le vainqueur ou la composition des deux Équipes à corriger.");
        if (winner != null && !Equipes.IsValid(winner))
            return CommandResult<CustomGame>.Invalid("Le vainqueur d'une game est une Équipe : « A » ou « B ».");
        if ((teamA == null) != (teamB == null))
            return CommandResult<CustomGame>.Invalid("La composition se corrige pour les deux Équipes à la fois.");

        var game = await _games.GetByIdWithParticipantsAsync(gameId);
        if (game == null)
            return CommandResult<CustomGame>.NotFound("Cette game n'existe pas.");
        if (game.Status != GameStatuses.Completed)
            return CommandResult<CustomGame>.Conflict("Cette game n'est pas terminée : saisissez son résultat.");

        Dictionary<int, string>? composition = null;
        if (teamA != null && teamB != null)
        {
            var (demandee, detail) = await ComposerAsync(game, teamA, teamB);
            if (detail != null) return CommandResult<CustomGame>.Invalid(detail);
            composition = demandee;
        }

        var concernes = game.Participants.Select(p => p.PlayerId).ToHashSet();
        var change = false;
        if (winner != null && winner != game.WinningTeam)
        {
            game.WinningTeam = winner;
            change = true;
        }
        if (composition != null && AppliquerComposition(game, composition))
        {
            change = true;
            concernes.UnionWith(composition.Keys);
        }
        if (!change) return CommandResult<CustomGame>.Ok(game);

        game.ModifieeParAdminLe = _clock.GetUtcNow().UtcDateTime;
        game.VainqueurOuCompositionCorriges = true;
        var series = game.SeriesId == null ? null : await _series.GetWithGamesAsync(game.SeriesId.Value);
        if (series != null)
        {
            series.RecalculerLesResultats(series.Games);
            series.Touch();
        }
        if (!await _games.TrySaveChangesAsync())
            return CommandResult<CustomGame>.Conflict(ModifieeEntreTemps);

        await RecalculerLesCostsAsync(concernes, game.PlayedAt);
        return CommandResult<CustomGame>.Ok(game);
    }

    /// <summary>
    /// Supprime une game terminée. Sa Série est recalculée ; sans autre game terminée, elle est effacée
    /// avec ses games en attente, comme une Série annulée.
    /// </summary>
    public async Task<CommandResult<bool>> SupprimerAsync(int gameId)
    {
        var game = await _games.GetByIdWithParticipantsAsync(gameId);
        if (game == null)
            return CommandResult<bool>.NotFound("Cette game n'existe pas.");
        if (game.Status != GameStatuses.Completed)
            return CommandResult<bool>.Conflict("Cette game n'est pas terminée : arrêtez la Série pour l'annuler.");

        var concernes = game.Participants.Select(p => p.PlayerId).ToHashSet();
        var playedAt = game.PlayedAt;
        var series = game.SeriesId == null ? null : await _series.GetWithGamesAsync(game.SeriesId.Value);
        _games.Remove(game);
        if (series != null)
        {
            var restantes = series.Games.Where(g => g.Id != game.Id).ToList();
            if (restantes.All(g => g.Status != GameStatuses.Completed))
            {
                foreach (var enAttente in restantes)
                    _games.Remove(enAttente);
                _series.Remove(series);
            }
            else
            {
                series.RecalculerLesResultats(restantes);
            }
            series.Touch();
        }
        if (!await _games.TrySaveChangesAsync())
            return CommandResult<bool>.Conflict(ModifieeEntreTemps);

        await RecalculerLesCostsAsync(concernes, playedAt);
        return CommandResult<bool>.Ok(true);
    }

    /// <summary>La composition demandée (joueur → Side) : dix joueurs distincts, cinq par Équipe, existants.</summary>
    private async Task<(Dictionary<int, string>? Composition, string? Detail)> ComposerAsync(
        CustomGame game, IReadOnlyCollection<int> teamA, IReadOnlyCollection<int> teamB)
    {
        if (teamA.Count != 5 || teamB.Count != 5)
            return (null, "Chaque Équipe compte cinq joueurs.");
        var tous = teamA.Concat(teamB).ToList();
        if (tous.Distinct().Count() != tous.Count)
            return (null, "Un joueur ne peut figurer qu'une fois dans la game.");
        if ((await _players.GetByIdsAsync(tous)).Count != tous.Count)
            return (null, "Un des joueurs n'existe pas.");

        var composition = teamA.ToDictionary(id => id, _ => game.TeamASide);
        foreach (var id in teamB)
            composition[id] = game.TeamBSide;
        return (composition, null);
    }

    /// <summary>Aligne les participants sur la composition ; renvoie true si elle a changé.</summary>
    private static bool AppliquerComposition(CustomGame game, Dictionary<int, string> composition)
    {
        var change = false;
        foreach (var retire in game.Participants.Where(p => !composition.ContainsKey(p.PlayerId)).ToList())
        {
            game.Participants.Remove(retire);
            change = true;
        }
        foreach (var (playerId, side) in composition)
        {
            var participant = game.Participants.SingleOrDefault(p => p.PlayerId == playerId);
            if (participant == null)
            {
                game.Participants.Add(new CustomGameParticipant { PlayerId = playerId, Side = side });
                change = true;
            }
            else if (participant.Side != side)
            {
                participant.Side = side;
                change = true;
            }
        }
        return change;
    }

    private async Task RecalculerLesCostsAsync(IEnumerable<int> playerIds, DateTime playedAt)
    {
        var mois = DateOnly.FromDateTime(playedAt);
        foreach (var playerId in playerIds)
            await _costs.RecalculatePlayerCost(playerId, mois);
    }
}
