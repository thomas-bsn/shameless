using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>
/// La Draft de champions d'une game. L'organisateur connecté l'ouvre pour la game en attente d'une
/// Série ; chaque capitaine joue, par son Lien capitaine, les bans et picks de son Équipe dans l'ordre
/// Tournament ; l'organisateur et les liens de la Série la lisent.
/// </summary>
public sealed class DraftChampionsService
{
    private readonly IDraftChampionsRepository _drafts;
    private readonly IBestOfSeriesRepository _series;
    private readonly IChampionCatalog _catalog;
    private readonly TimeProvider _clock;

    public DraftChampionsService(
        IDraftChampionsRepository drafts,
        IBestOfSeriesRepository series,
        IChampionCatalog catalog,
        TimeProvider clock)
    {
        _drafts = drafts;
        _series = series;
        _catalog = catalog;
        _clock = clock;
    }

    /// <summary>
    /// Ouvre la Draft de champions de la game en attente d'une Série en cours (son Side est connu),
    /// une fois les deux capitaines désignés. Une seule Draft de champions par game.
    /// </summary>
    public async Task<CommandResult<DraftChampions>> OuvrirAsync(int seriesId)
    {
        var series = await _series.GetWithGamesAsync(seriesId);
        if (series == null)
            return CommandResult<DraftChampions>.NotFound("Cette Série n'existe pas.");
        if (!series.IsEnCours)
            return CommandResult<DraftChampions>.Conflict("Cette Série est terminée : elle n'a plus de Draft de champions.");

        var game = series.Games.LastOrDefault(g => g.Status == GameStatuses.Pending);
        if (game == null)
            return CommandResult<DraftChampions>.Conflict(
                "Aucune game de cette Série n'est en attente : lancez la prochaine game avec son Side avant sa Draft de champions.");
        if (series.CapitaineOf(Equipes.A) == null || series.CapitaineOf(Equipes.B) == null)
            return CommandResult<DraftChampions>.Conflict(
                "Désignez le capitaine de chaque Équipe avant d'ouvrir la Draft de champions.");
        if (await _drafts.GetByGameAsync(game.Id) != null)
            return CommandResult<DraftChampions>.Conflict("La Draft de champions de cette game est déjà ouverte.");

        _drafts.Add(new DraftChampions { GameId = game.Id, TourCommenceLe = Maintenant() });
        if (!await _drafts.TrySaveChangesAsync())
            return CommandResult<DraftChampions>.Conflict("La Draft de champions de cette game est déjà ouverte.");

        return CommandResult<DraftChampions>.Ok((await _drafts.GetByGameAsync(game.Id))!);
    }

    /// <summary>La Draft de champions d'une game, en cours ou terminée.</summary>
    public async Task<CommandResult<DraftChampions>> GetByGameAsync(int gameId)
    {
        var draft = await _drafts.GetByGameAsync(gameId);
        return draft == null
            ? CommandResult<DraftChampions>.NotFound("Cette game n'a pas de Draft de champions.")
            : CommandResult<DraftChampions>.Ok(draft);
    }

    /// <summary>Par un lien de la Série : sa dernière Draft de champions, en cours ou terminée.</summary>
    public async Task<CommandResult<DraftChampions>> GetParLienAsync(AccesParLien acces)
    {
        var draft = await _drafts.GetDerniereOfSeriesAsync(acces.SeriesId);
        return draft == null || !acces.PermetDeLire(draft.Game.SeriesId!.Value)
            ? CommandResult<DraftChampions>.NotFound("Aucune Draft de champions n'est ouverte dans cette Série.")
            : CommandResult<DraftChampions>.Ok(draft);
    }

    /// <summary>
    /// Par un Lien capitaine : joue le tour <paramref name="tour"/> de la Draft de champions en cours de
    /// sa Série, pour son Équipe, avec ce champion (ban ou pick selon le tour).
    /// </summary>
    public async Task<CommandResult<DraftChampions>> JouerParLienAsync(AccesParLien acces, int tour, string? championId)
    {
        var draft = await _drafts.GetDerniereOfSeriesAsync(acces.SeriesId);
        if (draft == null)
            return CommandResult<DraftChampions>.NotFound("Aucune Draft de champions n'est ouverte dans cette Série.");

        return await JouerAsync(draft, tour, championId, equipe => acces.PermetDeJouer(draft.Game.SeriesId!.Value, equipe));
    }

    /// <summary>
    /// L'organisateur connecté joue le tour <paramref name="tour"/> à la place de l'Équipe attendue, une
    /// fois l'échéance du tour atteinte. Avant, le tour reste au capitaine.
    /// </summary>
    public async Task<CommandResult<DraftChampions>> JouerALaPlaceAsync(int gameId, int tour, string? championId)
    {
        var draft = await _drafts.GetByGameAsync(gameId);
        if (draft == null)
            return CommandResult<DraftChampions>.NotFound("Cette game n'a pas de Draft de champions.");

        return await JouerAsync(draft, tour, championId, _ => draft.EcheanceAtteinte(Maintenant()),
            equipe => $"Le capitaine de l'Équipe {equipe} a jusqu'à l'échéance du tour pour jouer : vous pourrez jouer à sa place ensuite.");
    }

    /// <summary>
    /// Joue le tour courant si c'est bien le tour <paramref name="tour"/>, que l'appelant peut jouer pour
    /// l'Équipe attendue, et que le champion existe et n'est ni banni ni pick dans la game.
    /// </summary>
    private async Task<CommandResult<DraftChampions>> JouerAsync(
        DraftChampions draft, int tour, string? championId, Func<string, bool> peutJouerPour,
        Func<string, string>? refus = null)
    {
        var courant = draft.TourCourant;
        if (courant == null || draft.Game.Status != GameStatuses.Pending)
            return CommandResult<DraftChampions>.Conflict("La Draft de champions de cette game est terminée.");
        if (tour < courant.Numero)
            return CommandResult<DraftChampions>.Conflict($"Le tour {tour} est déjà joué : c'est au tour {courant.Numero}.");
        if (tour > courant.Numero)
            return CommandResult<DraftChampions>.Conflict($"Ce n'est pas encore le tour {tour} : c'est au tour {courant.Numero}.");

        var equipe = draft.EquipeDuTour(courant.Numero);
        if (!peutJouerPour(equipe))
            return CommandResult<DraftChampions>.Conflict(refus?.Invoke(equipe) ?? $"Le tour {courant.Numero} revient à l'Équipe {equipe}.");

        var catalogue = await _catalog.GetAllAsync();
        if (string.IsNullOrEmpty(championId) || catalogue.All(c => c.Id != championId))
            return CommandResult<DraftChampions>.Invalid("Ce champion n'existe pas.");
        if (draft.AUtilise(championId))
            return CommandResult<DraftChampions>.Conflict("Ce champion est déjà banni ou pick dans cette game.");

        draft.Jouer(championId, Maintenant());
        if (!await _drafts.TrySaveChangesAsync())
            return CommandResult<DraftChampions>.Conflict(
                "Ce tour vient d'être joué par une autre commande : rechargez la Draft de champions.");

        return CommandResult<DraftChampions>.Ok(draft);
    }

    private DateTime Maintenant() => _clock.GetUtcNow().UtcDateTime;
}
