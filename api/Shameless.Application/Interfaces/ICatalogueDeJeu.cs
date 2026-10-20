namespace Shameless.Application.Interfaces;

/// <summary>Un objet du catalogue : identifiant du client, nom en français, icône.</summary>
public sealed record ObjetDuCatalogue(int Id, string Name, string IconUrl);

/// <summary>Un sort d'invocateur : identifiant numérique du client (<c>spell1Id</c>), nom en français, icône.</summary>
public sealed record SortDuCatalogue(int Id, string Name, string IconUrl);

/// <summary>Une rune : identifiant du client (<c>PERK0</c> à <c>PERK5</c>), nom en français, icône.</summary>
public sealed record RuneDuCatalogue(int Id, string Name, string IconUrl);

/// <summary>Un arbre de runes (<c>perkPrimaryStyle</c>, <c>perkSubStyle</c>) et ses runes.</summary>
public sealed record ArbreDuCatalogue(int Id, string Name, string IconUrl, IReadOnlyList<RuneDuCatalogue> Runes);

/// <summary>Une capacité d'un champion : touche (Q, W, E ou R), nom en français, icône.</summary>
public sealed record CapaciteDuCatalogue(string Touche, string Name, string IconUrl);

/// <summary>Un champion et ses quatre capacités ; <c>Id</c> est l'identifiant Data Dragon (« LeeSin »), celui du catalogue des champions.</summary>
public sealed record ChampionDuCatalogue(string Id, IReadOnlyList<CapaciteDuCatalogue> Capacites);

/// <summary>Ce que le site affiche des objets, sorts, runes et capacités de champions d'une game, pour la version courante du jeu.</summary>
public sealed record CatalogueDeJeu(
    string Version,
    IReadOnlyList<ObjetDuCatalogue> Objets,
    IReadOnlyList<SortDuCatalogue> Sorts,
    IReadOnlyList<ArbreDuCatalogue> Arbres,
    IReadOnlyList<ChampionDuCatalogue> Champions);

/// <summary>
/// Le catalogue des objets, sorts d'invocateur et runes (Data Dragon, en français). Les lectures d'une
/// game ne donnent que des identifiants du client : le site les traduit avec ce catalogue, et affiche
/// sans icône un identifiant qu'il ne connaît pas.
/// </summary>
public interface ICatalogueDeJeu
{
    Task<CatalogueDeJeu> GetAsync(CancellationToken cancellationToken = default);
}
