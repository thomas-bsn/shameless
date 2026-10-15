using Shameless.Application.Interfaces;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>Faux catalogue des champions : quelques champions fixes, sans appel à Data Dragon.</summary>
public sealed class FakeChampionCatalog : IChampionCatalog
{
    public static readonly IReadOnlyList<Champion> Champions =
    [
        new("Ahri", "103", "Ahri", "https://ddragon.test/img/champion/Ahri.png"),
        new("Garen", "86", "Garen", "https://ddragon.test/img/champion/Garen.png"),
        new("LeeSin", "64", "Lee Sin", "https://ddragon.test/img/champion/LeeSin.png"),
        new("Jinx", "222", "Jinx", "https://ddragon.test/img/champion/Jinx.png"),
        new("Thresh", "412", "Thresh", "https://ddragon.test/img/champion/Thresh.png")
    ];

    public Task<IReadOnlyList<Champion>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(Champions);
}
