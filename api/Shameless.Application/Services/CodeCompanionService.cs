using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>
/// Le code Companion d'une Série. L'organisateur connecté le lit et le renouvelle ;
/// <see cref="ResoudreAsync"/> traduit un code en sa Série, sans session : c'est par là que passe tout
/// ce que fait le Companion.
/// </summary>
public sealed class CodeCompanionService
{
    private readonly IBestOfSeriesRepository _series;

    public CodeCompanionService(IBestOfSeriesRepository series) => _series = series;

    /// <summary>La Série et son code, pour l'organisateur.</summary>
    public async Task<CommandResult<BestOfSeries>> GetAsync(int seriesId)
    {
        var series = await _series.GetByIdAsync(seriesId);
        return series == null
            ? CommandResult<BestOfSeries>.NotFound("Cette Série n'existe pas.")
            : CommandResult<BestOfSeries>.Ok(series);
    }

    /// <summary>Remplace le code Companion d'une Série en cours : l'ancien est refusé.</summary>
    public async Task<CommandResult<BestOfSeries>> RenouvelerAsync(int seriesId)
    {
        var series = await _series.GetByIdAsync(seriesId);
        if (series == null)
            return CommandResult<BestOfSeries>.NotFound("Cette Série n'existe pas.");
        if (!series.IsEnCours)
            return CommandResult<BestOfSeries>.Conflict(
                "Cette Série est terminée : son code Companion n'est plus valable.");

        series.RenouvelerCodeCompanion();
        series.Touch();
        return await _series.TrySaveChangesAsync()
            ? CommandResult<BestOfSeries>.Ok(series)
            : CommandResult<BestOfSeries>.Conflict(
                "Cette Série vient d'être modifiée par une autre commande : rechargez-la et réessayez.");
    }

    /// <summary>
    /// La Série de ce code, avec sa composition courante, ou null si le code ne vaut rien : inconnu,
    /// renouvelé, ou Série terminée (sauf <paramref name="serieTermineeAdmise"/>, pour répondre encore à
    /// ce que le Companion a envoyé avant la clôture).
    /// </summary>
    public async Task<BestOfSeries?> ResoudreAsync(string code, bool serieTermineeAdmise = false)
    {
        if (string.IsNullOrEmpty(code)) return null;
        var series = await _series.GetByCodeCompanionAsync(code);
        return series != null && (series.IsEnCours || serieTermineeAdmise) ? series : null;
    }
}
