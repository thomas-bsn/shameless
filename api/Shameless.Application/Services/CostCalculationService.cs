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
    /// Recalcule et enregistre le cost du Joueur pour ce mois, puis celui de chaque mois suivant déjà
    /// enregistré, qui repart du cost final du mois précédent. Le cost actuel du Joueur est celui de
    /// son mois le plus récent.
    /// </summary>
    public async Task RecalculatePlayerCost(int playerId, DateOnly month)
    {
        var player = await _playerRepo.GetByIdAsync(playerId);
        if (player == null) return;

        var historique = await _monthlyCostRepo.GetByPlayerAsync(playerId);
        await RecalculerDepuisAsync(player, historique, PremierJour(month));
    }

    /// <summary>
    /// Un admin fixe le cost du Joueur : il devient le cost de départ du mois en cours, que les Games
    /// du mois ajustent ensuite comme les autres, et dont repartent les mois suivants. Renvoie le cost
    /// actuel du Joueur, ou null si le Joueur n'existe pas.
    /// </summary>
    public async Task<decimal?> FixerCostAsync(int playerId, decimal cost)
    {
        var player = await _playerRepo.GetByIdAsync(playerId);
        if (player == null) return null;

        var historique = await _monthlyCostRepo.GetByPlayerAsync(playerId);
        var mois = PremierJour(DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime));
        var ligne = LigneDuMois(player.Id, historique, mois);
        ligne.CostDeDepart = cost;
        ligne.SourceDuCostDeDepart = SourceDuCostDeDepart.FixeParAdmin;

        await RecalculerDepuisAsync(player, historique, mois);
        return player.CurrentCost;
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

    /// <summary>
    /// Recalcule et enregistre ce mois puis, en cascade, chaque mois suivant de l'historique ; le
    /// dernier recalculé est le plus récent : c'est le cost actuel du Joueur.
    /// </summary>
    private async Task RecalculerDepuisAsync(Player player, List<PlayerMonthlyCost> historique, DateOnly depuis)
    {
        var aRecalculer = historique.Select(MoisDe).Where(m => m > depuis).Append(depuis).Distinct().Order().ToList();
        CostMensuel? cost = null;
        foreach (var mois in aRecalculer)
        {
            cost = await CalculerAsync(player, historique, mois);
            var ligne = LigneDuMois(player.Id, historique, mois);
            ligne.FinalCost = cost.Cost;
            ligne.CostDeDepart = cost.Depart.Cost;
            ligne.SourceDuCostDeDepart = cost.Depart.Source;
            ligne.NoteMoyenne = cost.NoteMoyenne;
            ligne.GamesPlayed = cost.GamesTerminees;
            ligne.Wins = cost.Victoires;
            await _monthlyCostRepo.UpsertAsync(ligne);
        }

        player.CurrentCost = cost!.Cost;
        player.IsPlacement = !cost.Evolue;
        await _playerRepo.UpdateAsync(player);
    }

    /// <summary>La ligne du mois dans l'historique, ajoutée si elle n'y est pas encore.</summary>
    private static PlayerMonthlyCost LigneDuMois(int playerId, List<PlayerMonthlyCost> historique, DateOnly mois)
    {
        var ligne = historique.FirstOrDefault(m => MoisDe(m) == mois);
        if (ligne is not null) return ligne;

        ligne = new PlayerMonthlyCost { PlayerId = playerId, Month = mois.ToDateTime(TimeOnly.MinValue) };
        historique.Add(ligne);
        return ligne;
    }

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
    /// Le cost fixé par un admin pour ce mois ; sinon le cost final du dernier mois enregistré avant
    /// celui-ci ; sans historique, le Rang SoloQ, figé au premier calcul du mois.
    /// </summary>
    private static CostDeDepart Depart(Player player, List<PlayerMonthlyCost> historique, DateOnly mois)
    {
        var duMois = historique.FirstOrDefault(m => MoisDe(m) == mois);
        if (duMois is { SourceDuCostDeDepart: SourceDuCostDeDepart.FixeParAdmin, CostDeDepart: { } fixe })
            return CostDeDepart.FixeParAdmin(fixe);

        var precedent = historique.Where(m => MoisDe(m) < mois).MaxBy(m => m.Month);
        if (precedent is not null)
            return CostDeDepart.DuMoisPrecedent(precedent.FinalCost);

        return duMois?.CostDeDepart is { } cost
            ? new CostDeDepart(cost, SourceDuCostDeDepart.RangSoloQ)
            : CostDeDepart.DuRangSoloQ(player.RankTier, player.RankDivision);
    }

    private static DateOnly MoisDe(PlayerMonthlyCost ligne) => DateOnly.FromDateTime(ligne.Month);

    private static DateOnly PremierJour(DateOnly date) => new(date.Year, date.Month, 1);
}