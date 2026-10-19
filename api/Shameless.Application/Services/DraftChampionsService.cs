using Shameless.Application.ChampionDraft;
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
        if (game.RapportEnRevue != null)
            return CommandResult<DraftChampions>.Conflict(
                "Le rapport de la game en attente est en revue : la Draft de champions attend la décision de l'organisateur.");
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
        return draft == null
            ? CommandResult<DraftChampions>.NotFound("Aucune Draft de champions n'est ouverte dans cette Série.")
            : CommandResult<DraftChampions>.Ok(draft);
    }

    /// <summary>
    /// Par un Lien capitaine : joue le tour <paramref name="tour"/> de la dernière Draft de champions de
    /// la Série <paramref name="seriesId"/>, pour le capitaine de son Équipe, avec ce champion (ban ou
    /// pick selon le tour).
    /// </summary>
    public async Task<CommandResult<DraftChampions>> JouerParLienAsync(
        int seriesId, JoueurDraft.Capitaine capitaine, int tour, string? championId)
    {
        var draft = await _drafts.GetDerniereOfSeriesAsync(seriesId);
        if (draft == null)
            return CommandResult<DraftChampions>.NotFound("Aucune Draft de champions n'est ouverte dans cette Série.");

        return await JouerAsync(draft, tour, championId, capitaine);
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

        return await JouerAsync(draft, tour, championId, new JoueurDraft.Organisateur());
    }

    /// <summary>
    /// Joue le tour courant si c'est bien le tour <paramref name="tour"/>, que le
    /// <paramref name="joueur"/> peut le jouer (le capitaine de l'Équipe attendue, ou l'organisateur
    /// passé l'échéance), et que le champion existe et n'est ni banni ni pick dans la game.
    /// </summary>
    private async Task<CommandResult<DraftChampions>> JouerAsync(
        DraftChampions draft, int tour, string? championId, JoueurDraft joueur)
    {
        var courant = draft.TourCourant;
        if (courant == null)
            return CommandResult<DraftChampions>.Conflict("La Draft de champions de cette game est terminée.");
        if (tour < courant.Numero)
            return CommandResult<DraftChampions>.Conflict($"Le tour {tour} est déjà joué : c'est au tour {courant.Numero}.");
        if (tour > courant.Numero)
            return CommandResult<DraftChampions>.Conflict($"Ce n'est pas encore le tour {tour} : c'est au tour {courant.Numero}.");

        var equipe = draft.EquipeDuTour(courant.Numero);
        var refus = joueur switch
        {
            JoueurDraft.Capitaine capitaine when capitaine.Equipe != equipe =>
                $"Le tour {courant.Numero} revient à l'Équipe {equipe}.",
            JoueurDraft.Organisateur when !draft.EcheanceAtteinte(Maintenant()) =>
                $"Le capitaine de l'Équipe {equipe} a jusqu'à l'échéance du tour pour jouer : vous pourrez jouer à sa place ensuite.",
            _ => null
        };
        if (refus != null)
            return CommandResult<DraftChampions>.Conflict(refus);

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

    /// <summary>
    /// Le Capitaine de l'Équipe <paramref name="equipe"/> demande à remettre la draft en cours à zéro ;
    /// l'autre Capitaine a <see cref="DraftChampions.DelaiReponseReset"/> pour répondre. Refusé une fois la
    /// draft terminée, ou si une demande attend déjà sa réponse.
    /// </summary>
    public async Task<CommandResult<bool>> DemanderResetAsync(int seriesId, string equipe)
    {
        var draft = await _drafts.GetDerniereOfSeriesAsync(seriesId);
        if (draft == null)
            return CommandResult<bool>.Conflict("Aucune draft n'est en cours dans cette Série.");
        if (draft.EstTerminee)
            return CommandResult<bool>.Conflict("La draft est terminée : elle ne peut plus être remise à zéro.");
        if (draft.ResetEnAttente(Maintenant()))
            return CommandResult<bool>.Conflict("Une demande de Reset est déjà en attente de réponse.");

        draft.DemanderReset(equipe, Maintenant());
        return await EnregistrerAsync(draft);
    }

    /// <summary>
    /// Le Capitaine de l'Équipe <paramref name="equipe"/> répond à la demande de Reset de l'autre Équipe.
    /// Accepté : la draft est effacée (bans, picks, chrono) et les deux Capitaines redeviennent « pas prêts » ;
    /// la Game reste la même. Refusé : la demande tombe, rien d'autre ne change.
    /// </summary>
    public async Task<CommandResult<bool>> RepondreResetAsync(int seriesId, string equipe, bool accepte)
    {
        var draft = await _drafts.GetDerniereOfSeriesAsync(seriesId);
        if (draft == null)
            return CommandResult<bool>.Conflict("Aucune draft n'est en cours dans cette Série.");
        if (draft.EstTerminee)
            return CommandResult<bool>.Conflict("La draft est terminée : elle ne peut plus être remise à zéro.");
        if (!draft.ResetEnAttente(Maintenant()))
            return CommandResult<bool>.Conflict("Aucune demande de Reset n'attend de réponse.");
        if (draft.ResetDemandeParEquipe == equipe)
            return CommandResult<bool>.Conflict("Seul l'autre Capitaine peut répondre à ta demande de Reset.");

        if (!accepte)
        {
            draft.AnnulerDemandeReset();
            return await EnregistrerAsync(draft);
        }

        var series = await _series.GetWithGamesAsync(seriesId);
        series?.RemettrePasPrets();
        series?.Touch();
        _drafts.Remove(draft);
        return await EnregistrerAsync(draft);
    }

    private async Task<CommandResult<bool>> EnregistrerAsync(DraftChampions draft) =>
        await _drafts.TrySaveChangesAsync()
            ? CommandResult<bool>.Ok(true)
            : CommandResult<bool>.Conflict("La draft vient d'être modifiée par une autre commande : réessayez.");

    // Tronqué à la microseconde, la précision de Postgres : l'échéance renvoyée juste après
    // une action est ainsi la même que celle relue en base ensuite.
    private DateTime Maintenant()
    {
        var maintenant = _clock.GetUtcNow().UtcDateTime;
        return maintenant.AddTicks(-(maintenant.Ticks % 10));
    }
}
