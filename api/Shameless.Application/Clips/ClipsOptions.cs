namespace Shameless.Application.Clips;

/// <summary>Section de configuration <c>Clips</c> : le stockage des vidéos de clip sur l'Instance (ADR 0014).</summary>
public sealed class ClipsOptions
{
    public const string Section = "Clips";

    /// <summary>
    /// Le dossier où les vidéos sont conservées : un volume de l'Instance (<c>/clips</c> dans le compose).
    /// Créé au besoin.
    /// </summary>
    public string StoragePath { get; set; } = "clips";

    /// <summary>Taille maximale d'une vidéo déposée, en octets : 500 Mo par défaut (limite Discord Nitro).</summary>
    public long MaxFileSizeBytes { get; set; } = 500L * 1024 * 1024;

    /// <summary>Les types de vidéo acceptés, avec l'extension du fichier conservé.</summary>
    public static readonly IReadOnlyDictionary<string, string> Formats = new Dictionary<string, string>
    {
        ["video/mp4"] = ".mp4",
        ["video/webm"] = ".webm",
        ["video/quicktime"] = ".mov"
    };
}

/// <summary>
/// Section de configuration <c>Instance</c> : ce qui est propre à la communauté, ici son fuseau horaire,
/// qui découpe les jours (jour de publication d'un clip, ADR 0014).
/// </summary>
public sealed class InstanceOptions
{
    public const string Section = "Instance";

    /// <summary>Fuseau IANA de l'Instance (<c>SHAMELESS_TIMEZONE</c>), <c>Europe/Paris</c> par défaut.</summary>
    public string TimeZone { get; set; } = "Europe/Paris";

    public TimeZoneInfo Fuseau() => TimeZoneInfo.FindSystemTimeZoneById(TimeZone);
}
