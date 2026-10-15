namespace Shameless.Application.Interfaces;

/// <summary>
/// Source d'aléa des tirages (Picker…). Injectée pour qu'un test puisse fixer la graine.
/// </summary>
public interface IRandomSource
{
    /// <summary>Entier dans [0, <paramref name="maxExclusive"/>[.</summary>
    int Next(int maxExclusive);
}
