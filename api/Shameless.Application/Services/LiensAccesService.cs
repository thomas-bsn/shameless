using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>
/// Les liens d'accès d'une Série (ADR 0010). L'organisateur connecté les lit, renouvelle un Lien
/// capitaine. Le Lien spectateur est disponible dès la création de la Série (ADR 0017).
/// <see cref="ResoudreAsync"/> traduit un lien en l'accès qu'il donne, sans session : c'est par là que
/// passent les commandes et lectures ouvertes aux liens.
/// </summary>
public sealed class LiensAccesService
{
    private readonly IBestOfSeriesRepository _series;
    private readonly TimeProvider _clock;

    public LiensAccesService(IBestOfSeriesRepository series, TimeProvider clock)
    {
        _series = series;
        _clock = clock;
    }

    /// <summary>La Série et ses liens, pour l'organisateur.</summary>
    public async Task<CommandResult<BestOfSeries>> GetAsync(int seriesId)
    {
        var series = await _series.GetWithLiensAsync(seriesId);
        return series == null
            ? CommandResult<BestOfSeries>.NotFound("Cette Série n'existe pas.")
            : CommandResult<BestOfSeries>.Ok(series);
    }

    /// <summary>Remplace le Lien capitaine de l'Équipe : l'ancien ne donne plus accès à rien.</summary>
    public async Task<CommandResult<BestOfSeries>> RenouvelerLienCapitaineAsync(int seriesId, string equipe)
    {
        if (!Equipes.IsValid(equipe))
            return CommandResult<BestOfSeries>.Invalid("Un Lien capitaine est celui de l'Équipe A ou de l'Équipe B.");

        var series = await _series.GetWithLiensAsync(seriesId);
        if (series == null)
            return CommandResult<BestOfSeries>.NotFound("Cette Série n'existe pas.");

        series.LienCapitaine(equipe).Renouveler();
        return await SaveAsync(series);
    }

    /// <summary>
    /// Le bot rapporte que le DM du Lien capitaine de l'Équipe n'est pas parti (compte Discord non lié,
    /// DM fermé…) : l'organisateur le voit sur l'Écran de préparation.
    /// </summary>
    public async Task<CommandResult<BestOfSeries>> SignalerEchecDmAsync(int seriesId, string equipe, string raison)
    {
        if (!Equipes.IsValid(equipe))
            return CommandResult<BestOfSeries>.Invalid("Un Lien capitaine est celui de l'Équipe A ou de l'Équipe B.");
        if (!LienAcces.RaisonsEchecDm.Contains(raison))
            return CommandResult<BestOfSeries>.Invalid(
                $"La raison d'un échec de DM est {string.Join(", ", LienAcces.RaisonsEchecDm)}.");

        var series = await _series.GetWithLiensAsync(seriesId);
        if (series == null)
            return CommandResult<BestOfSeries>.NotFound("Cette Série n'existe pas.");
        if (series.CapitaineOf(equipe) is not { } capitaine)
            return CommandResult<BestOfSeries>.Conflict("Cette Équipe n'a pas de Capitaine : aucun DM n'a pu partir.");

        series.LienCapitaine(equipe).SignalerEchecDm(raison, capitaine, _clock.GetUtcNow().UtcDateTime);
        return await SaveAsync(series);
    }

    /// <summary>
    /// L'accès que donne ce lien, ou null s'il n'en donne aucun : lien inconnu ou renouvelé.
    /// </summary>
    public async Task<AccesParLien?> ResoudreAsync(string secret)
    {
        if (string.IsNullOrEmpty(secret)) return null;
        var lien = await _series.GetLienAsync(secret);
        return lien == null ? null : lien.Series.AccesPar(lien);
    }

    /// <summary>
    /// Le Capitaine ouvre son lien : sa première ouverture est mémorisée (ADR 0017), les suivantes ne
    /// changent rien. Le Lien spectateur ne fait rejoindre personne.
    /// </summary>
    public async Task MarquerRejointAsync(string secret)
    {
        var lien = await _series.GetLienAsync(secret);
        if (lien is not { Portee: PorteeLien.Capitaine, RejointLe: null }) return;

        lien.Rejoindre(_clock.GetUtcNow().UtcDateTime);
        await _series.TrySaveChangesAsync();
    }

    private async Task<CommandResult<BestOfSeries>> SaveAsync(BestOfSeries series)
    {
        series.Touch();
        return await _series.TrySaveChangesAsync()
            ? CommandResult<BestOfSeries>.Ok(series)
            : CommandResult<BestOfSeries>.Conflict(
                "Cette Série vient d'être modifiée par une autre commande : rechargez-la et réessayez.");
    }
}
