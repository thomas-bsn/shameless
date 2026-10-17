using System.Globalization;

namespace Shameless.Application.Discord;

/// <summary>
/// Un ID Discord (snowflake) : un entier strictement positif, <c>long</c> en base, toujours en texte
/// dans les contrats (trop grand pour un nombre JSON).
/// </summary>
public readonly record struct DiscordId
{
    public long Valeur { get; }

    public DiscordId(long valeur)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(valeur);
        Valeur = valeur;
    }

    /// <summary>L'ID Discord écrit en texte (chiffres seuls, > 0), ou null s'il n'en est pas un.</summary>
    public static DiscordId? Lire(string? texte) =>
        long.TryParse(texte, NumberStyles.None, CultureInfo.InvariantCulture, out var valeur) && valeur > 0
            ? new DiscordId(valeur)
            : null;

    /// <summary>L'ID Discord en texte, tel que les contrats le transmettent.</summary>
    public static string Texte(long valeur) => new DiscordId(valeur).ToString();

    public override string ToString() => Valeur.ToString(CultureInfo.InvariantCulture);
}
