using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

public enum CloseSoireeResult
{
    Closed,
    NotFound,
    AlreadyClosed
}

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

    /// <summary>Ouvre une Soirée. Renvoie null si une Soirée est déjà ouverte.</summary>
    public async Task<Soiree?> OpenAsync()
    {
        if (await _soirees.GetOpenAsync() != null) return null;

        var soiree = new Soiree { Status = SoireeStatus.Open, OpenedAt = _clock.GetUtcNow().UtcDateTime };
        return await _soirees.TryAddAsync(soiree) ? soiree : null;
    }

    public async Task<(CloseSoireeResult Result, Soiree? Soiree)> CloseAsync(int id)
    {
        var soiree = await _soirees.GetByIdAsync(id);
        if (soiree == null) return (CloseSoireeResult.NotFound, null);
        if (soiree.Status == SoireeStatus.Closed) return (CloseSoireeResult.AlreadyClosed, soiree);

        soiree.Status = SoireeStatus.Closed;
        soiree.ClosedAt = _clock.GetUtcNow().UtcDateTime;
        await _soirees.SaveChangesAsync();
        return (CloseSoireeResult.Closed, soiree);
    }

    /// <summary>
    /// Ferme la Soirée ouverte si sa dernière activité (dernière game terminée, sinon l'ouverture)
    /// date de plus que <paramref name="delai"/>. Renvoie la Soirée fermée, ou null.
    /// </summary>
    public async Task<Soiree?> CloseIfIdleAsync(TimeSpan delai)
    {
        var soiree = await _soirees.GetOpenWithSeriesAndGamesAsync();
        if (soiree == null) return null;
        if (soiree.Series.Any(EstEnCours)) return null;

        var derniereGameTerminee = soiree.Series
            .SelectMany(s => s.Games)
            .Where(g => g.Status == "completed")
            .Select(g => (DateTime?)g.PlayedAt)
            .Max();
        var derniereActivite = derniereGameTerminee ?? soiree.OpenedAt;

        var now = _clock.GetUtcNow().UtcDateTime;
        if (now - derniereActivite <= delai) return null;

        soiree.Status = SoireeStatus.Closed;
        soiree.ClosedAt = now;
        await _soirees.SaveChangesAsync();
        return soiree;
    }

    /// <summary>
    /// Une Série est en cours tant qu'aucune équipe n'a atteint le score de victoire de son format.
    /// Seul endroit qui en décide : à remplacer par l'issue explicite de la Série quand elle existera (#19, #20).
    /// </summary>
    private static bool EstEnCours(BestOfSeries serie)
    {
        var victoiresNecessaires = serie.Format / 2 + 1;
        return serie.TeamAWins < victoiresNecessaires && serie.TeamBWins < victoiresNecessaires;
    }
}
