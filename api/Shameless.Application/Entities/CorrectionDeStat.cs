namespace Shameless.Application.Entities;

/// <summary>Les champs d'un participant qu'un Administrateur peut corriger dans une game terminée.</summary>
public enum ChampCorrigeable
{
    Champion,
    Position,
    Kills,
    Deaths,
    Assists,
    Cs,
    GoldEarned,
    DamageToChampions,
    VisionScore,
    Note
}

public static class ChampCorrigeableExtensions
{
    /// <summary>Le nom du champ dans l'API : camelCase (ex. « damageToChampions »).</summary>
    public static string Nom(this ChampCorrigeable champ) =>
        System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(champ.ToString());

    /// <summary>Le champ de ce nom d'API ; false s'il n'est pas corrigeable.</summary>
    public static bool TryParse(string? nom, out ChampCorrigeable champ)
    {
        foreach (var candidat in Enum.GetValues<ChampCorrigeable>())
        {
            if (candidat.Nom() != nom) continue;
            champ = candidat;
            return true;
        }
        champ = default;
        return false;
    }
}

/// <summary>
/// La correction d'un champ des stats d'un joueur dans une game terminée, stockée à part : une par
/// champ et par participant. Les données brutes du Companion (<c>RiotParticipantData</c>, champion du
/// rapport) ne sont jamais modifiées ; l'affichage prend la valeur corrigée quand elle existe, et
/// supprimer la correction rétablit la valeur d'origine.
/// </summary>
public class CorrectionDeStat
{
    public int Id { get; set; }
    public int CustomGameParticipantId { get; set; }
    public ChampCorrigeable Champ { get; set; }

    /// <summary>La valeur corrigée : un nombre pour une stat ou la note, un texte pour le champion (id Data Dragon) et la voie (rôle).</summary>
    public string Valeur { get; set; } = string.Empty;

    public CustomGameParticipant CustomGameParticipant { get; set; } = null!;
}
