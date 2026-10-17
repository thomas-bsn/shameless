using Microsoft.Extensions.Options;
using Shameless.Application.Clips;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;

namespace Shameless.Application.Services;

/// <summary>
/// Les Clips (ADR 0014) : dépôt par le bot, rattachement automatique à l'unique Game plausible du jour
/// de publication, lecture depuis la Game.
/// </summary>
public sealed class ClipsService
{
    private readonly IClipRepository _clips;
    private readonly IStockageDesClips _stockage;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _fuseau;

    public ClipsService(IClipRepository clips, IStockageDesClips stockage, TimeProvider clock, IOptions<InstanceOptions> instance)
    {
        _clips = clips;
        _stockage = stockage;
        _clock = clock;
        _fuseau = instance.Value.Fuseau();
    }

    /// <summary>
    /// Conserve la vidéo et ses métadonnées, et rattache le clip si une unique Game plausible existe.
    /// Idempotent sur la pièce jointe Discord : un dépôt renvoyé rend le clip déjà conservé
    /// (<c>Nouveau</c> false), sans rien réécrire.
    /// </summary>
    public async Task<CommandResult<(ClipDuBot Clip, bool Nouveau)>> DeposerAsync(DepotDeClip depot, Stream video, CancellationToken ct)
    {
        if (await _clips.GetByAttachmentIdAsync(depot.AttachmentId) is { } deja)
            return CommandResult<(ClipDuBot, bool)>.Ok((ClipDuBot.From(deja), false));

        if (!ClipsOptions.Formats.TryGetValue(depot.ContentType, out var extension))
            return CommandResult<(ClipDuBot, bool)>.Invalid("Un clip est une vidéo mp4, webm ou mov.");
        if (depot.Taille <= 0)
            return CommandResult<(ClipDuBot, bool)>.Invalid("La vidéo du clip est vide.");
        if (string.IsNullOrWhiteSpace(depot.AuteurNom))
            return CommandResult<(ClipDuBot, bool)>.Invalid("Le nom de l'auteur du clip est requis.");

        var publieLe = depot.PublieLe.ToUniversalTime();
        var jour = GamePlausible.JourDePublication(publieLe, _fuseau);
        var gameId = GamePlausible.Unique(publieLe, _fuseau, await _clips.GetGamesLanceesEntreAsync(jour.Debut, publieLe));

        var fichier = await _stockage.EnregistrerAsync(video, extension, ct);
        var clip = new Clip
        {
            DiscordMessageId = depot.MessageId,
            DiscordAttachmentId = depot.AttachmentId,
            AuteurDiscordId = depot.AuteurDiscordId,
            AuteurNom = depot.AuteurNom.Trim(),
            PublieLe = publieLe,
            RecuLe = _clock.GetUtcNow().UtcDateTime,
            NomFichier = Path.GetFileName(depot.NomFichier),
            ContentType = depot.ContentType,
            Taille = depot.Taille,
            Fichier = fichier
        };
        if (gameId is { } id) clip.RattacherA(id, RattachementsClip.Automatique);
        _clips.Add(clip);

        if (await _clips.TrySaveChangesAsync())
            return CommandResult<(ClipDuBot, bool)>.Ok((ClipDuBot.From(clip), true));

        // Le même dépôt, reçu en même temps : on garde le premier.
        _stockage.Supprimer(fichier);
        return await _clips.GetByAttachmentIdAsync(depot.AttachmentId) is { } premier
            ? CommandResult<(ClipDuBot, bool)>.Ok((ClipDuBot.From(premier), false))
            : CommandResult<(ClipDuBot, bool)>.Conflict("Le clip n'a pas pu être enregistré, réessayer.");
    }

    /// <summary>Les clips rattachés à la game, ou null si elle n'existe pas.</summary>
    public async Task<List<ClipResponse>?> GetDeLaGameAsync(int gameId) =>
        await _clips.GameExisteAsync(gameId)
            ? (await _clips.GetDeLaGameAsync(gameId)).Select(ClipResponse.From).ToList()
            : null;

    /// <summary>La vidéo du clip, ou null si le clip ou son fichier n'existe pas.</summary>
    public async Task<FichierDeClip?> GetFichierAsync(int clipId)
    {
        if (await _clips.GetAsync(clipId) is not { } clip) return null;
        var chemin = _stockage.CheminDe(clip.Fichier);
        return File.Exists(chemin) ? new FichierDeClip(chemin, clip.ContentType, clip.NomFichier) : null;
    }
}
