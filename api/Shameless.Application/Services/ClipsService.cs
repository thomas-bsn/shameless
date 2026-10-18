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
    private readonly ICustomGameRepository _games;
    private readonly IStockageDesClips _stockage;
    private readonly TimeProvider _clock;
    private readonly TimeZoneInfo _fuseau;

    public ClipsService(
        IClipRepository clips, ICustomGameRepository games, IStockageDesClips stockage, TimeProvider clock,
        IOptions<InstanceOptions> instance)
    {
        _clips = clips;
        _games = games;
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
        var (debut, fin) = GamePlausible.FenetreDesPlausibles(publieLe, _fuseau);
        var gameId = GamePlausible.Unique(await _games.GetIdsLanceesEntreAsync(debut, fin));

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

        bool enregistre;
        try
        {
            enregistre = await _clips.TrySaveChangesAsync();
        }
        catch
        {
            // Pas de vidéo orpheline : sans ses métadonnées, personne ne la retrouverait.
            _stockage.Supprimer(fichier);
            throw;
        }
        if (enregistre)
            return CommandResult<(ClipDuBot, bool)>.Ok((ClipDuBot.From(clip), true));

        // Le même dépôt, reçu en même temps : on garde le premier.
        _stockage.Supprimer(fichier);
        return await _clips.GetByAttachmentIdAsync(depot.AttachmentId) is { } premier
            ? CommandResult<(ClipDuBot, bool)>.Ok((ClipDuBot.From(premier), false))
            : CommandResult<(ClipDuBot, bool)>.Conflict("Le clip n'a pas pu être enregistré : réessayez.");
    }

    /// <summary>Les clips rattachés à la game, ou null si elle n'existe pas.</summary>
    public async Task<List<ClipResponse>?> GetDeLaGameAsync(int gameId) =>
        await _games.ExisteAsync(gameId)
            ? (await _clips.GetDeLaGameAsync(gameId)).Select(ClipResponse.From).ToList()
            : null;

    /// <summary>Les clips qui attendent un rattachement manuel, du plus ancien au plus récent publié.</summary>
    public async Task<List<ClipResponse>> GetSansGameAsync() =>
        (await _clips.GetSansGameAsync()).Select(ClipResponse.From).ToList();

    public const int TailleMaxDePage = 200;

    /// <summary>
    /// Une page de tous les clips, du plus récent au plus ancien publié. <paramref name="soiree"/> est un
    /// jour calendaire dans le fuseau de l'Instance : la soirée d'un clip est le jour de lancement de sa
    /// Game, ou celui de sa publication sans Game.
    /// </summary>
    public async Task<CommandResult<ClipsPage>> ListerAsync(string? auteur, DateOnly? soiree, bool? sansGame, int page, int taille)
    {
        if (page < 1) return CommandResult<ClipsPage>.Invalid("La page commence à 1.");
        if (taille is < 1 or > TailleMaxDePage)
            return CommandResult<ClipsPage>.Invalid($"Une page compte de 1 à {TailleMaxDePage} clips.");

        DateTime? debut = null, fin = null;
        if (soiree is { } jour)
        {
            // Midi du jour désigné : le jour calendaire qui le contient, dans le fuseau de l'Instance.
            var midi = TimeZoneInfo.ConvertTimeToUtc(jour.ToDateTime(new TimeOnly(12, 0), DateTimeKind.Unspecified), _fuseau);
            (debut, fin) = GamePlausible.JourDePublication(midi, _fuseau);
        }

        var (clips, total) = await _clips.ListerAsync(new FiltreDesClips(auteur, debut, fin, sansGame), page, taille);
        return CommandResult<ClipsPage>.Ok(new ClipsPage(clips.Select(ClipDeLaListe.From).ToList(), total, page, taille));
    }

    /// <summary>Les clips éligibles au Fond (non masqués), dans un ordre aléatoire.</summary>
    public async Task<List<ClipDuFond>> GetDuFondAsync() =>
        (await _clips.GetDuFondAuHasardAsync()).Select(ClipDuFond.From).ToList();

    /// <summary>
    /// Les games à proposer pour rattacher le clip à la main, de la plus récente à la plus ancienne, ou
    /// null si le clip n'existe pas (<see cref="GamePlausible.FenetreDesCandidates"/>).
    /// </summary>
    public async Task<List<GameCandidateDuClip>?> GetGamesCandidatesAsync(int clipId)
    {
        if (await _clips.GetAsync(clipId) is not { } clip) return null;
        var (debut, fin) = GamePlausible.FenetreDesCandidates(clip.PublieLe, _fuseau);
        return (await _games.GetLanceesEntreAsync(debut, fin)).Select(GameCandidateDuClip.From).ToList();
    }

    /// <summary>
    /// Rattache le clip à cette Game, à la main : un clip sans Game, ou dont le rattachement était faux.
    /// </summary>
    public async Task<CommandResult<ClipResponse>> RattacherAsync(int clipId, int gameId)
    {
        if (await _clips.GetAsync(clipId) is not { } clip)
            return CommandResult<ClipResponse>.NotFound("Ce clip n'existe pas.");
        if (!await _games.ExisteAsync(gameId))
            return CommandResult<ClipResponse>.NotFound("Cette game n'existe pas.");

        clip.RattacherA(gameId, RattachementsClip.Manuel);
        return await EnregistrerAsync(clip);
    }

    /// <summary>Retire le clip de sa Game : il revient parmi les clips sans Game. Sans effet s'il n'en a pas.</summary>
    public async Task<CommandResult<ClipResponse>> DetacherAsync(int clipId)
    {
        if (await _clips.GetAsync(clipId) is not { } clip)
            return CommandResult<ClipResponse>.NotFound("Ce clip n'existe pas.");

        clip.Detacher();
        return await EnregistrerAsync(clip);
    }

    /// <summary>Masque le clip du Fond (<paramref name="masque"/> vrai) ou le réaffiche. Idempotent.</summary>
    public async Task<CommandResult<MasqueDuFondDuClip>> MasquerDuFondAsync(int clipId, bool masque)
    {
        if (await _clips.GetAsync(clipId) is not { } clip)
            return CommandResult<MasqueDuFondDuClip>.NotFound("Ce clip n'existe pas.");

        clip.MasqueDuFond = masque;
        return await _clips.TrySaveChangesAsync()
            ? CommandResult<MasqueDuFondDuClip>.Ok(new MasqueDuFondDuClip(clip.Id, clip.MasqueDuFond))
            : CommandResult<MasqueDuFondDuClip>.Conflict("Le clip n'a pas pu être mis à jour : réessayez.");
    }

    private async Task<CommandResult<ClipResponse>> EnregistrerAsync(Clip clip) =>
        await _clips.TrySaveChangesAsync()
            ? CommandResult<ClipResponse>.Ok(ClipResponse.From(clip))
            : CommandResult<ClipResponse>.Conflict("Le rattachement du clip n'a pas pu être enregistré : réessayez.");

    /// <summary>La vidéo du clip, ou null si le clip ou son fichier n'existe pas.</summary>
    public async Task<FichierDeClip?> GetFichierAsync(int clipId)
    {
        if (await _clips.GetAsync(clipId) is not { } clip) return null;
        return _stockage.Trouver(clip.Fichier) is { } chemin ? new FichierDeClip(chemin, clip.ContentType, clip.NomFichier) : null;
    }
}
