using Shameless.Application.Entities;
using Shameless.Application.Discord;

namespace Shameless.Application.Clips;

/// <summary>Ce que le bot dépose pour une vidéo de <c>#clips</c>, la vidéo à part (ADR 0014).</summary>
public sealed record DepotDeClip(
    long MessageId,
    long AttachmentId,
    long AuteurDiscordId,
    string AuteurNom,
    DateTime PublieLe,
    string NomFichier,
    string ContentType,
    long Taille);

/// <summary>
/// Le clip déposé, tel que le bot le lit (<c>POST /bot/clips</c>).
/// </summary>
/// <param name="GameId">La Game du clip, null s'il attend un rattachement manuel.</param>
/// <param name="Rattachement">« automatique » ou « manuel », null sans Game.</param>
public sealed record ClipDuBot(int ClipId, int? GameId, string? Rattachement)
{
    public static ClipDuBot From(Clip clip) => new(clip.Id, clip.GameId, clip.RattachementEffectif);
}

/// <summary>
/// Un clip tel que le site l'affiche. La vidéo se lit sur <c>GET /clips/{id}/fichier</c>.
/// </summary>
/// <param name="AuteurDiscordId">L'auteur du message Discord (snowflake, en texte).</param>
/// <param name="GameId">La Game du clip, null s'il attend un rattachement manuel.</param>
/// <param name="Rattachement">« automatique » ou « manuel », null sans Game.</param>
/// <param name="Taille">Taille de la vidéo, en octets.</param>
public sealed record ClipResponse(
    int Id,
    int? GameId,
    string? Rattachement,
    string AuteurNom,
    string AuteurDiscordId,
    DateTime PublieLe,
    string NomFichier,
    string ContentType,
    long Taille)
{
    public static ClipResponse From(Clip clip) => new(
        clip.Id,
        clip.GameId,
        clip.RattachementEffectif,
        clip.AuteurNom,
        DiscordId.Texte(clip.AuteurDiscordId),
        clip.PublieLe,
        clip.NomFichier,
        clip.ContentType,
        clip.Taille);
}

/// <summary>
/// Une Game proposée pour rattacher un clip à la main (<c>GET /clips/{id}/games-candidates</c>).
/// </summary>
/// <param name="SeriesId">La Série de la game, null pour une game legacy hors Série.</param>
/// <param name="GameNumber">Le rang de la game dans sa Série, à partir de 1.</param>
/// <param name="BestOf">Le format de la Série (BO1, BO3…), null hors Série.</param>
/// <param name="LanceeLe">Quand la game a été lancée (UTC).</param>
public sealed record GameCandidateDuClip(int GameId, int? SeriesId, int GameNumber, int? BestOf, DateTime LanceeLe)
{
    public static GameCandidateDuClip From(CustomGame game) =>
        new(game.Id, game.SeriesId, game.NumeroDansLaSerie, game.Series?.Format, game.CreatedAt);
}

/// <summary>La vidéo d'un clip, à servir : chemin absolu et type.</summary>
public sealed record FichierDeClip(string Chemin, string ContentType, string NomFichier);
