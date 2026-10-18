namespace Shameless.Application.Interfaces;

/// <summary>Un champion du catalogue : identifiant Data Dragon (ex. "LeeSin"), clé numérique Riot, nom affiché, image.</summary>
public sealed record Champion(string Id, string Key, string Name, string ImageUrl, IReadOnlyList<string>? Roles = null);

/// <summary>Le catalogue des champions jouables, pour la Draft de champions. Données statiques publiques.</summary>
public interface IChampionCatalog
{
    /// <summary>Tous les champions, triés par nom.</summary>
    Task<IReadOnlyList<Champion>> GetAllAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Le catalogue des champions est indisponible (Data Dragon en panne, réponse illisible ou vide) :
/// l'API répond 503 à toute requête qui en a besoin.
/// </summary>
public sealed class CatalogueIndisponibleException(string message, Exception? innerException = null)
    : Exception(message, innerException);
