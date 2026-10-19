using System.Collections.Concurrent;

namespace Shameless.Application.Services;

/// <summary>Un Companion connecté : le PUUID du compte de son client, sa Série et depuis combien de temps on l'a entendu.</summary>
public sealed record CompanionConnecte(string Puuid, int SeriesId, TimeSpan VuIlYA);

/// <summary>
/// La présence des Companions (ADR 0010) : le dernier signal de chaque compte LoL, gardé en mémoire
/// seulement. Un redémarrage de l'API la vide, les signaux suivants la reconstruisent. Un Companion est
/// connecté tant que son dernier signal date de moins de <see cref="DelaiDeConnexion"/>.
/// </summary>
public sealed class PresenceCompanionService(TimeProvider clock)
{
    public static readonly TimeSpan DelaiDeConnexion = TimeSpan.FromSeconds(90);

    private readonly ConcurrentDictionary<string, (int SeriesId, DateTimeOffset Vu)> _signaux = new();

    public void Signaler(string puuid, int seriesId) => _signaux[puuid] = (seriesId, clock.GetUtcNow());

    /// <summary>Les Companions connectés, du plus récemment entendu au plus ancien ; les signaux périmés sont oubliés.</summary>
    public IReadOnlyList<CompanionConnecte> Connectes()
    {
        var maintenant = clock.GetUtcNow();
        foreach (var (puuid, signal) in _signaux)
            if (maintenant - signal.Vu >= DelaiDeConnexion)
                _signaux.TryRemove(new KeyValuePair<string, (int, DateTimeOffset)>(puuid, signal));

        return _signaux
            .Select(s => new CompanionConnecte(s.Key, s.Value.SeriesId, maintenant - s.Value.Vu))
            .Where(c => c.VuIlYA < DelaiDeConnexion)
            .OrderBy(c => c.VuIlYA)
            .ToList();
    }

    /// <summary>Oublie tout signal (les tests repartent d'une présence vide).</summary>
    public void Vider() => _signaux.Clear();
}
