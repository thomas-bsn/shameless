using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>
/// Les liens d'accès d'une Série (ADR 0010). L'organisateur connecté les lit, renouvelle un Lien
/// capitaine et indique avoir envoyé les Liens capitaine, ce qui rend le Lien spectateur disponible.
/// <see cref="ResoudreAsync"/> traduit un lien en l'accès qu'il donne, sans session : c'est par là que
/// passent les commandes et lectures ouvertes aux liens.
/// </summary>
public sealed class LiensAccesService
{
    private readonly IBestOfSeriesRepository _series;

    public LiensAccesService(IBestOfSeriesRepository series) => _series = series;

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

    /// <summary>L'organisateur a envoyé les Liens capitaine : le Lien spectateur devient disponible.</summary>
    public async Task<CommandResult<BestOfSeries>> IndiquerLiensCapitaineEnvoyesAsync(int seriesId)
    {
        var series = await _series.GetWithLiensAsync(seriesId);
        if (series == null)
            return CommandResult<BestOfSeries>.NotFound("Cette Série n'existe pas.");
        if (series.LiensCapitaineEnvoyes)
            return CommandResult<BestOfSeries>.Ok(series);

        series.LiensCapitaineEnvoyes = true;
        return await SaveAsync(series);
    }

    /// <summary>
    /// L'accès que donne ce lien, ou null s'il n'en donne aucun : lien inconnu, renouvelé, ou Lien
    /// spectateur pas encore disponible.
    /// </summary>
    public async Task<AccesParLien?> ResoudreAsync(string secret)
    {
        if (string.IsNullOrEmpty(secret)) return null;
        var lien = await _series.GetLienAsync(secret);
        return lien == null ? null : lien.Series.AccesPar(lien);
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
