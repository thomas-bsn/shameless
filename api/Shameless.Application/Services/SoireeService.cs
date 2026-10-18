using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>Les commandes d'une Soirée : l'ouvrir, la fermer à la main ou après une période sans game.</summary>
public sealed class SoireeService
{
    private readonly ISoireeRepository _soirees;
    private readonly TimeProvider _clock;

    public SoireeService(ISoireeRepository soirees, TimeProvider clock)
    {
        _soirees = soirees;
        _clock = clock;
    }

    public Task<Soiree?> GetOpenAsync() => _soirees.GetOpenAsync();

    /// <summary>Dit si une fermeture automatique a interrompu le Picker.</summary>
    public async Task<bool> PickerInterrompuAsync() =>
        (await _soirees.GetDerniereFermeeAsync())?.Picker is not null;

    public Task<Soiree?> GetAsync(int id) => _soirees.GetByIdAsync(id);

    /// <summary>Ouvre une Soirée ; refusé si une Soirée est déjà ouverte.</summary>
    public async Task<CommandResult<Soiree>> OpenAsync()
    {
        if (await _soirees.GetOpenAsync() != null) return DejaOuverte();

        var soiree = new Soiree { Status = SoireeStatus.Open, OpenedAt = _clock.GetUtcNow().UtcDateTime };
        return await _soirees.TryAddAsync(soiree) ? CommandResult<Soiree>.Ok(soiree) : DejaOuverte();
    }

    /// <summary>Ferme une Soirée ouverte ; refusé tant qu'une de ses Séries est en cours.</summary>
    public async Task<CommandResult<Soiree>> CloseAsync(int id)
    {
        var soiree = await _soirees.GetWithSeriesAsync(id);
        if (soiree == null)
            return CommandResult<Soiree>.NotFound("Cette Soirée n'existe pas.");
        if (soiree.Status == SoireeStatus.Closed)
            return CommandResult<Soiree>.Conflict("Cette Soirée est déjà fermée.");
        if (soiree.Series.Any(s => s.IsEnCours))
            return CommandResult<Soiree>.Conflict(
                "Une Série de cette Soirée est en cours : arrêtez la Série d'abord.");

        soiree.Status = SoireeStatus.Closed;
        soiree.ClosedAt = _clock.GetUtcNow().UtcDateTime;
        await _soirees.SaveChangesAsync();
        return CommandResult<Soiree>.Ok(soiree);
    }

    /// <summary>
    /// Ferme la Soirée ouverte si sa dernière activité (saisie du résultat de la dernière game, sinon l'ouverture)
    /// date de plus que <paramref name="delai"/>. Renvoie la Soirée fermée, ou null.
    /// </summary>
    public async Task<Soiree?> CloseIfIdleAsync(TimeSpan delai)
    {
        var soiree = await _soirees.GetOpenWithSeriesAndGamesAsync();
        if (soiree == null) return null;
        if (soiree.Series.Any(s => s.IsEnCours)) return null;

        var derniereGameTerminee = soiree.Series
            .SelectMany(s => s.Games)
            .Where(g => g.Status == GameStatuses.Completed)
            .Select(g => g.CompletedAt ?? g.PlayedAt)
            .Cast<DateTime?>()
            .Max();
        var derniereActivite = derniereGameTerminee ?? soiree.OpenedAt;

        var now = _clock.GetUtcNow().UtcDateTime;
        if (now - derniereActivite <= delai) return null;

        soiree.Status = SoireeStatus.Closed;
        soiree.ClosedAt = now;
        soiree.FermetureAutomatique = true;
        await _soirees.SaveChangesAsync();
        return soiree;
    }

    private static CommandResult<Soiree> DejaOuverte() => CommandResult<Soiree>.Conflict("Une Soirée est déjà ouverte.");
}
