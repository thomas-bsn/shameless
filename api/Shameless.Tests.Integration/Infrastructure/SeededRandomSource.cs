using Shameless.Application.Interfaces;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>Aléa à graine fixe, rejoué depuis le début à chaque test ; un tirage peut être imposé.</summary>
public sealed class SeededRandomSource(int seed) : IRandomSource
{
    private Random _random = new(seed);
    private int? _impose;

    public int Next(int maxExclusive)
    {
        if (_impose is { } valeur)
        {
            _impose = null;
            return valeur;
        }
        return _random.Next(maxExclusive);
    }

    /// <summary>Le prochain tirage vaut <paramref name="valeur"/>, une seule fois.</summary>
    public void ImposerLeProchainTirage(int valeur) => _impose = valeur;

    /// <summary>Annule un tirage imposé que personne n'a consommé.</summary>
    public void LibererLeTirage() => _impose = null;

    public void Reset()
    {
        _random = new Random(seed);
        _impose = null;
    }
}
