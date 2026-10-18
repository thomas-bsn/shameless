using Shameless.Application.Clips;
using Shameless.Application.Entities;

namespace Shameless.Application.Interfaces;

/// <summary>Les Clips et leurs métadonnées (la vidéo est dans <see cref="IStockageDesClips"/>).</summary>
public interface IClipRepository
{
    void Add(Clip clip);

    Task<Clip?> GetAsync(int id);

    /// <summary>Le clip déposé pour cette pièce jointe Discord, tel qu'enregistré en base, ou null.</summary>
    Task<Clip?> GetByAttachmentIdAsync(long discordAttachmentId);

    /// <summary>Les clips rattachés à la game, du plus ancien au plus récent publié.</summary>
    Task<List<Clip>> GetDeLaGameAsync(int gameId);

    /// <summary>Les clips sans Game, du plus ancien au plus récent publié.</summary>
    Task<List<Clip>> GetSansGameAsync();

    /// <summary>
    /// Une page des clips qui correspondent au filtre, du plus récent au plus ancien publié, avec leur
    /// Game et sa Série, et le nombre total de clips correspondants. La soirée d'un clip est le jour de
    /// lancement de sa Game, ou son jour de publication sans Game.
    /// </summary>
    Task<(List<Clip> Clips, int Total)> ListerAsync(FiltreDesClips filtre, int page, int taille);

    /// <summary>Au plus <paramref name="limite"/> clips non masqués du Fond, tirés au hasard par la base.</summary>
    Task<List<Clip>> GetDuFondAuHasardAsync(int limite);

    /// <summary>Enregistre ; false si une autre commande a déposé la même pièce jointe entre-temps.</summary>
    Task<bool> TrySaveChangesAsync();
}
