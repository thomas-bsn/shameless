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

    /// <summary>Les games (id, lancée le) lancées dans l'intervalle [debut, fin] (UTC).</summary>
    Task<List<(int Id, DateTime LanceeLe)>> GetGamesLanceesEntreAsync(DateTime debut, DateTime fin);

    Task<bool> GameExisteAsync(int gameId);

    /// <summary>Enregistre ; false si une autre commande a déposé la même pièce jointe entre-temps.</summary>
    Task<bool> TrySaveChangesAsync();
}
