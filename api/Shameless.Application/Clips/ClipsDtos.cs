using Shameless.Application.Entities;

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
        clip.AuteurDiscordId.ToString(System.Globalization.CultureInfo.InvariantCulture),
        clip.PublieLe,
        clip.NomFichier,
        clip.ContentType,
        clip.Taille);
}

/// <summary>La vidéo d'un clip, à servir : chemin absolu et type.</summary>
public sealed record FichierDeClip(string Chemin, string ContentType, string NomFichier);
