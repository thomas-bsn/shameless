namespace Shameless.Application.Entities;

/// <summary>
/// Une vidéo d'un moment de game publiée dans <c>#clips</c>, téléchargée par le bot et conservée dans le
/// stockage de l'Instance (ADR 0014). Rattachée à une Game, ou sans Game en attendant un rattachement
/// manuel. La vidéo vit dans un fichier ; la ligne en garde les métadonnées.
/// </summary>
public class Clip
{
    public int Id { get; set; }

    /// <summary>Le message Discord qui porte la vidéo (snowflake).</summary>
    public long DiscordMessageId { get; set; }

    /// <summary>La pièce jointe Discord (snowflake), unique : un même dépôt renvoyé ne crée pas de doublon.</summary>
    public long DiscordAttachmentId { get; set; }

    /// <summary>L'auteur du message (snowflake), souvent sans Utilisateur ni Joueur dans Shameless.</summary>
    public long AuteurDiscordId { get; set; }

    /// <summary>Le nom affiché de l'auteur sur Discord au moment de la publication.</summary>
    public string AuteurNom { get; set; } = "";

    /// <summary>Quand le message a été publié dans <c>#clips</c> (UTC) : c'est ce jour qui désigne la Game plausible.</summary>
    public DateTime PublieLe { get; set; }

    /// <summary>Quand l'API a reçu le dépôt (UTC).</summary>
    public DateTime RecuLe { get; set; }

    /// <summary>Le nom du fichier sur Discord, tel que publié.</summary>
    public string NomFichier { get; set; } = "";

    public string ContentType { get; set; } = "";

    /// <summary>Taille de la vidéo, en octets.</summary>
    public long Taille { get; set; }

    /// <summary>Le nom du fichier dans le stockage des clips (jamais choisi par le bot).</summary>
    public string Fichier { get; set; } = "";

    /// <summary>La Game du clip, null s'il attend un rattachement manuel.</summary>
    public int? GameId { get; set; }
    public CustomGame? Game { get; set; }

    /// <summary>
    /// Comment le clip a été rattaché (<see cref="RattachementsClip"/>), null sans Game. Si la Game
    /// disparaît (game en attente d'une Série arrêtée), le clip reste, sans Game.
    /// </summary>
    public string? Rattachement { get; set; }

    /// <summary>Un clip masqué reste accessible dans la liste et sa Game, mais ne joue plus dans le Fond.</summary>
    public bool MasqueDuFond { get; set; }

    /// <summary>Le rattachement effectif : null dès que le clip n'a plus de Game.</summary>
    public string? RattachementEffectif => GameId == null ? null : Rattachement;

    /// <summary>Rattache le clip à cette Game.</summary>
    public void RattacherA(int gameId, string rattachement)
    {
        GameId = gameId;
        Rattachement = rattachement;
    }

    /// <summary>Retire le clip de sa Game : il attend de nouveau un rattachement manuel.</summary>
    public void Detacher()
    {
        GameId = null;
        Rattachement = null;
    }
}

/// <summary>Les façons dont un clip est rattaché à sa Game.</summary>
public static class RattachementsClip
{
    /// <summary>À son dépôt, parce qu'une unique Game plausible existait le jour de publication.</summary>
    public const string Automatique = "automatique";

    /// <summary>Par un Utilisateur connecté, depuis le site (#64).</summary>
    public const string Manuel = "manuel";
}
