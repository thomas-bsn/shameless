using Shameless.Application.Interfaces;

namespace Shameless.Tests.Integration.Infrastructure;

/// <summary>
/// Faux catalogue des objets, sorts et runes : quelques entrées fixes, sans appel à Data Dragon.
/// <see cref="Indisponible"/> le rend indisponible, comme Data Dragon en panne.
/// </summary>
public sealed class FakeCatalogueDeJeu : ICatalogueDeJeu
{
    public static readonly CatalogueDeJeu Catalogue = new(
        "15.1.1",
        [
            new(6655, "Rayon de Luden", "https://ddragon.test/cdn/15.1.1/img/item/6655.png"),
            new(3020, "Bottes du sorcier", "https://ddragon.test/cdn/15.1.1/img/item/3020.png"),
            new(1203, "Quête : Voie du mage", "https://ddragon.test/cdn/15.1.1/img/item/1203.png")
        ],
        [
            new(4, "Saut éclair", "https://ddragon.test/cdn/15.1.1/img/spell/SummonerFlash.png"),
            new(14, "Embrasement", "https://ddragon.test/cdn/15.1.1/img/spell/SummonerDot.png")
        ],
        [
            new(8100, "Domination", "https://ddragon.test/cdn/img/perk-images/Styles/7200_Domination.png",
                [new(8112, "Électrocution", "https://ddragon.test/cdn/img/perk-images/Styles/Domination/Electrocute/Electrocute.png")])
        ]);

    /// <summary>Le catalogue est indisponible ; remis à disposition avant chaque test.</summary>
    public bool Indisponible { get; set; }

    public void Reset() => Indisponible = false;

    public Task<CatalogueDeJeu> GetAsync(CancellationToken cancellationToken = default) =>
        Indisponible
            ? throw new CatalogueIndisponibleException("Data Dragon ne répond pas.")
            : Task.FromResult(Catalogue);
}
