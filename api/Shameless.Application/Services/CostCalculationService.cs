using Shameless.Application.Cost;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
using Shameless.Application.Stats;
using Shameless.Infrastructure.Repositories;

namespace Shameless.Application.Services;

/// <summary>Le cost d'un Joueur pour un mois, avec le détail du calcul.</summary>
public sealed record CostDuMois(Player Player, DateOnly Mois, CostMensuel Calcul);

/// <summary>
/// Le cost mensuel des Joueurs (ADR 0012) : recalculé après chaque Game terminée et conservé, mois
/// par mois, dans <see cref="PlayerMonthlyCost"/>. Le calcul lui-même est <see cref="CostMensuel"/>.
/// </summary>
public class CostCalculationService
{
    private readonly ICustomGameRepository _gameRepo;
    private readonly IPlayerRepository _playerRepo;
    private readonly IPlayerMonthlyCostRepository _monthlyCostRepo;
    private readonly TimeProvider _clock;

    public CostCalculationService(
        ICustomGameRepository gameRepo,
        IPlayerRepository playerRepo,
        IPlayerMonthlyCostRepository monthlyCostRepo,
        TimeProvider clock)
    {
        _gameRepo = gameRepo;
        _playerRepo = playerRepo;
        _monthlyCostRepo = monthlyCostRepo;
        _clock = clock;
    }

    /// <summary>Recalcule le cost du mois de la game (sa date de lancement) pour chacun de ses participants.</summary>
    public async Task RecalculateCosts(int gameId)
    {
        var game = await _gameRepo.GetByIdWithParticipantsAsync(gameId);
        if (game == null) return;

        var mois = DateOnly.FromDateTime(game.PlayedAt);
        foreach (var playerId in game.Participants.Select(p => p.PlayerId).ToList())
            await RecalculatePlayerCost(playerId, mois);
    }

    /// <summary>
    /// Recalcule et enregistre le cost du Joueur pour ce mois. S'il s'agit de son mois le plus récent,
    /// c'est aussi son cost actuel.
    /// </summary>
    public async Task RecalculatePlayerCost(int playerId, DateOnly month)
    {
        var player = await _playerRepo.GetByIdAsync(playerId);
        if (player == null) return;

        var historique = await _monthlyCostRepo.GetByPlayerAsync(playerId);
        var mois = PremierJour(month);
        var cost = await CalculerAsync(player, historique, mois);

        var enregistre = historique.FirstOrDefault(m => DateOnly.FromDateTime(m.Month) == mois)
                         ?? new PlayerMonthlyCost { PlayerId = playerId, Month = mois.ToDateTime(TimeOnly.MinValue) };
        enregistre.FinalCost = cost.Cost;
        enregistre.CostDeDepart = cost.Depart.Cost;
        enregistre.NoteMoyenne = cost.NoteMoyenne;
        enregistre.GamesPlayed = cost.GamesTerminees;
        enregistre.Wins = cost.Victoires;
        await _monthlyCostRepo.SaveAsync(enregistre);

        if (historique.All(m => DateOnly.FromDateTime(m.Month) <= mois))
        {
            player.CurrentCost = cost.Cost;
            player.IsPlacement = !cost.Evolue;
            await _playerRepo.UpdateAsync(player);
        }
    }

    /// <summary>
    /// Le cost du Joueur pour le mois (par défaut le mois en cours), calculé depuis ses games terminées ;
    /// null si le Joueur n'existe pas.
    /// </summary>
    public async Task<CostDuMois?> CalculatePlayerCostPreview(int playerId, DateOnly? month = null)
    {
        var player = await _playerRepo.GetByIdAsync(playerId);
        if (player == null) return null;

        var mois = PremierJour(month ?? DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime));
        var historique = await _monthlyCostRepo.GetByPlayerAsync(playerId);
        return new CostDuMois(player, mois, await CalculerAsync(player, historique, mois));
    }

    /// <summary>L'historique de cost du Joueur, du plus ancien mois au plus récent ; null si le Joueur n'existe pas.</summary>
    public async Task<List<PlayerMonthlyCost>?> HistoriqueAsync(int playerId) =>
        await _playerRepo.GetByIdAsync(playerId) == null ? null : await _monthlyCostRepo.GetByPlayerAsync(playerId);

    private async Task<CostMensuel> CalculerAsync(Player player, List<PlayerMonthlyCost> historique, DateOnly mois)
    {
        var games = await _gameRepo.GetCompletedGamesOfPlayerForMonthAsync(player.Id, mois.ToDateTime(TimeOnly.MinValue));
        var gamesDuMois = games
            .Select(g => new GameDuMois(
                g.Participants.Single(p => p.PlayerId == player.Id).HasWon,
                NotesDeLaGame.DuJoueur(g, player.Id)?.Note))
            .ToList();
        return CostMensuel.Calculer(Depart(player, historique, mois), gamesDuMois);
    }

    /// <summary>
    /// Le cost final du dernier mois enregistré avant celui-ci ; sans historique, le Rang SoloQ, figé
    /// au premier calcul du mois.
    /// </summary>
    private static CostDeDepart Depart(Player player, List<PlayerMonthlyCost> historique, DateOnly mois)
    {
        var precedent = historique.Where(m => DateOnly.FromDateTime(m.Month) < mois).MaxBy(m => m.Month);
        if (precedent is not null)
            return CostDeDepart.DuMoisPrecedent(precedent.FinalCost);

        var dejaCalcule = historique.FirstOrDefault(m => DateOnly.FromDateTime(m.Month) == mois)?.CostDeDepart;
        return dejaCalcule is { } cost
            ? new CostDeDepart(cost, SourceDuCostDeDepart.RangSoloQ)
            : CostDeDepart.DuRangSoloQ(player.RankTier, player.RankDivision);
    }

    private static DateOnly PremierJour(DateOnly date) => new(date.Year, date.Month, 1);
}
