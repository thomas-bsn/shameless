namespace Shameless.Application.Interfaces;

/// <summary>Un champion du catalogue : identifiant Data Dragon (ex. "LeeSin"), clé numérique Riot, nom affiché, image.</summary>
public sealed record Champion(string Id, string Key, string Name, string ImageUrl);

/// <summary>Le catalogue des champions jouables, pour la Draft de champions. Données statiques publiques.</summary>
public interface IChampionCatalog
{
    /// <summary>Tous les champions, triés par nom.</summary>
    Task<IReadOnlyList<Champion>> GetAllAsync(CancellationToken cancellationToken = default);
}
