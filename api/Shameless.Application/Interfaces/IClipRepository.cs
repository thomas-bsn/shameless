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

    Task<List<Clip>> GetTousAsync(bool? sansGame, bool? masques, string? auteur, int? soireeId, int page, int pageSize);

    Task<List<Clip>> GetEligiblesAuFondAsync(int take);

    /// <summary>Enregistre ; false si une autre commande a déposé la même pièce jointe entre-temps.</summary>
    Task<bool> TrySaveChangesAsync();
}
