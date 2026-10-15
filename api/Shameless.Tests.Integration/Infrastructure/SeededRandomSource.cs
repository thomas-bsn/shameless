using Shameless.Application.Interfaces;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>Aléa à graine fixe, rejoué depuis le début à chaque test.</summary>
public sealed class SeededRandomSource(int seed) : IRandomSource
{
    private Random _random = new(seed);

    public int Next(int maxExclusive) => _random.Next(maxExclusive);

    public void Reset() => _random = new Random(seed);
}
