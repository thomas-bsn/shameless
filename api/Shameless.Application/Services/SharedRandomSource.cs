using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>Aléa de production : <see cref="Random.Shared"/>, sûr entre threads.</summary>
public sealed class SharedRandomSource : IRandomSource
{
    public int Next(int maxExclusive) => Random.Shared.Next(maxExclusive);
}
