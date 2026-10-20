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
    Note,
    ChampLevel,
    Spell1Id,
    Spell2Id,
    LargestKillingSpree,
    KillingSprees,
    DoubleKills,
    TripleKills,
    QuadraKills,
    PentaKills,
    MinionsKilled,
    NeutralMinionsKilled,
    GoldSpent,
    PhysicalDamageToChampions,
    MagicDamageToChampions,
    TrueDamageToChampions,
    DamageTaken,
    TotalHeal,
    DamageShielded,
    TimeCcingOthers,
    WardsPlaced,
    WardsKilled,
    ControlWardsBought,
    DamageToObjectives,
    TurretKills,
    DragonKills,
    BaronKills,
    InhibitorKills,
    RoleBoundItem,
    PerkPrimaryStyle,
    PerkSubStyle,
    Item0,
    Item1,
    Item2,
    Item3,
    Item4,
    Item5,
    Item6,
    Perk0,
    Perk1,
    Perk2,
    Perk3,
    Perk4,
    Perk5
}

/// <summary>Ce qu'un champ accepte : un nombre entre <paramref name="Min"/> et <paramref name="Max"/>, ou un texte (champion, voie).</summary>
public readonly record struct DefinitionDeChamp(bool Texte, int Min, int Max);

public static class ChampCorrigeableExtensions
{
    /// <summary>Le nom du champ dans l'API : camelCase (ex. « damageToChampions »).</summary>
    public static string Nom(this ChampCorrigeable champ) =>
        System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(champ.ToString());

    private const int StatMax = 1_000_000;
    private const int IdentifiantMax = 99_999;

    /// <summary>Ce que le champ accepte ; la note va de 0 à 100, le niveau de 1 à 18, les objets, sorts et runes sont des identifiants du client (0 = vide).</summary>
    public static DefinitionDeChamp Definition(this ChampCorrigeable champ) => champ switch
    {
        ChampCorrigeable.Champion or ChampCorrigeable.Position => new(true, 0, 0),
        ChampCorrigeable.Note => new(false, 0, 100),
        ChampCorrigeable.ChampLevel => new(false, 1, 18),
        ChampCorrigeable.LargestKillingSpree or ChampCorrigeable.KillingSprees or ChampCorrigeable.DoubleKills
            or ChampCorrigeable.TripleKills or ChampCorrigeable.QuadraKills or ChampCorrigeable.PentaKills
            or ChampCorrigeable.TurretKills or ChampCorrigeable.DragonKills or ChampCorrigeable.BaronKills
            or ChampCorrigeable.InhibitorKills => new(false, 0, 99),
        ChampCorrigeable.Spell1Id or ChampCorrigeable.Spell2Id or ChampCorrigeable.RoleBoundItem
            or ChampCorrigeable.PerkPrimaryStyle or ChampCorrigeable.PerkSubStyle
            or >= ChampCorrigeable.Item0 => new(false, 0, IdentifiantMax),
        _ => new(false, 0, StatMax)
    };

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
