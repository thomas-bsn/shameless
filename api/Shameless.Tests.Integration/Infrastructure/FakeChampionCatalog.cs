using Shameless.Application.Interfaces;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>
/// Faux catalogue des champions : vingt-cinq champions fixes, assez pour une Draft de champions complète,
/// sans appel à Data Dragon. <see cref="Indisponible"/> le rend indisponible, comme Data Dragon en panne.
/// </summary>
public sealed class FakeChampionCatalog : IChampionCatalog
{
    public static readonly IReadOnlyList<Champion> Champions =
    [
        new("Ahri", "103", "Ahri", "https://ddragon.test/img/champion/Ahri.png", ["Mage", "Assassin"]),
        new("Garen", "86", "Garen", "https://ddragon.test/img/champion/Garen.png"),
        new("LeeSin", "64", "Lee Sin", "https://ddragon.test/img/champion/LeeSin.png"),
        new("Jinx", "222", "Jinx", "https://ddragon.test/img/champion/Jinx.png"),
        new("Thresh", "412", "Thresh", "https://ddragon.test/img/champion/Thresh.png"),
        new("Annie", "1", "Annie", "https://ddragon.test/img/champion/Annie.png"),
        new("Ashe", "22", "Ashe", "https://ddragon.test/img/champion/Ashe.png"),
        new("Braum", "201", "Braum", "https://ddragon.test/img/champion/Braum.png"),
        new("Darius", "122", "Darius", "https://ddragon.test/img/champion/Darius.png"),
        new("Ezreal", "81", "Ezreal", "https://ddragon.test/img/champion/Ezreal.png"),
        new("Fiora", "114", "Fiora", "https://ddragon.test/img/champion/Fiora.png"),
        new("Galio", "3", "Galio", "https://ddragon.test/img/champion/Galio.png"),
        new("Irelia", "39", "Irelia", "https://ddragon.test/img/champion/Irelia.png"),
        new("Janna", "40", "Janna", "https://ddragon.test/img/champion/Janna.png"),
        new("Karma", "43", "Karma", "https://ddragon.test/img/champion/Karma.png"),
        new("Lulu", "117", "Lulu", "https://ddragon.test/img/champion/Lulu.png"),
        new("Malphite", "54", "Malphite", "https://ddragon.test/img/champion/Malphite.png"),
        new("Nami", "267", "Nami", "https://ddragon.test/img/champion/Nami.png"),
        new("Orianna", "61", "Orianna", "https://ddragon.test/img/champion/Orianna.png"),
        new("Pyke", "555", "Pyke", "https://ddragon.test/img/champion/Pyke.png"),
        new("Riven", "92", "Riven", "https://ddragon.test/img/champion/Riven.png"),
        new("Sona", "37", "Sona", "https://ddragon.test/img/champion/Sona.png"),
        new("Teemo", "17", "Teemo", "https://ddragon.test/img/champion/Teemo.png"),
        new("Vayne", "67", "Vayne", "https://ddragon.test/img/champion/Vayne.png"),
        new("Zed", "238", "Zed", "https://ddragon.test/img/champion/Zed.png")
    ];

    /// <summary>Le catalogue est indisponible ; remis à disposition avant chaque test.</summary>
    public bool Indisponible { get; set; }

    public void Reset() => Indisponible = false;

    public Task<IReadOnlyList<Champion>> GetAllAsync(CancellationToken cancellationToken = default) =>
        Indisponible
            ? throw new CatalogueIndisponibleException("Data Dragon ne répond pas.")
            : Task.FromResult(Champions);
}
