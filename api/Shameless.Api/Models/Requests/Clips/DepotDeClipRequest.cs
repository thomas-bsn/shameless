using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Shameless.Application.Clips;

namespace Shameless.Api.Models.Requests.Clips;

/// <summary>
/// Le dépôt d'une vidéo de <c>#clips</c> par le bot, en <c>multipart/form-data</c> : la pièce jointe
/// téléchargée (<c>fichier</c>, avec son type, ex. <c>video/mp4</c>) et le message qui la porte. Les
/// snowflakes Discord sont en texte. <c>publieLe</c> est l'heure de publication du message (ISO 8601).
/// </summary>
public sealed class DepotDeClipRequest
{
    [FromForm(Name = "messageId")]
    public string MessageId { get; set; } = "";
    [FromForm(Name = "attachmentId")]
    public string AttachmentId { get; set; } = "";
    [FromForm(Name = "auteurDiscordId")]
    public string AuteurDiscordId { get; set; } = "";
    [FromForm(Name = "auteurNom")]
    public string AuteurNom { get; set; } = "";
    [FromForm(Name = "publieLe")]
    public DateTimeOffset PublieLe { get; set; }
    [FromForm(Name = "fichier")]
    public IFormFile? Fichier { get; set; }

    /// <summary>Le dépôt, ou la raison (en français) pour laquelle il est mal formé.</summary>
    public (DepotDeClip? Depot, string? Refus) ToDepot()
    {
        if (!Snowflake(MessageId, out var message) || !Snowflake(AttachmentId, out var attachment) || !Snowflake(AuteurDiscordId, out var auteur))
            return (null, "messageId, attachmentId et auteurDiscordId sont des identifiants Discord (snowflakes, en texte).");
        if (PublieLe == default)
            return (null, "publieLe, l'heure de publication du message, est requise.");
        if (Fichier is null)
            return (null, "La vidéo du clip (fichier) est requise.");

        return (new DepotDeClip(message, attachment, auteur, AuteurNom ?? "", PublieLe.UtcDateTime,
            Fichier.FileName, Fichier.ContentType ?? "", Fichier.Length), null);
    }

    private static bool Snowflake(string? texte, out long valeur) =>
        long.TryParse(texte, NumberStyles.None, CultureInfo.InvariantCulture, out valeur) && valeur > 0;
}
