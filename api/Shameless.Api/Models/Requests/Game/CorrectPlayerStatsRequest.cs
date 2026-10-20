using Shameless.Application.Entities;
using Shameless.Application.Services;

namespace Shameless.Api.Contracts.Matches;

/// <summary>
/// La correction des stats d'un joueur dans une game terminée. Chaque champ absent reste tel quel ; une
/// correction remplace la valeur du Companion à l'affichage, sans la modifier. Les objets, sorts et runes
/// sont des identifiants du client LoL (0 pour un emplacement vide).
/// </summary>
public class CorrectPlayerStatsRequest
{
    /// <summary>Le champion joué (id Data Dragon, ex. « LeeSin »).</summary>
    public string? Champion { get; set; }

    /// <summary>La voie jouée : Top, Jungle, Mid, Adc ou Support.</summary>
    public string? Position { get; set; }

    public int? Kills { get; set; }

    public int? Deaths { get; set; }

    public int? Assists { get; set; }

    /// <summary>Le CS total (sbires et monstres neutres).</summary>
    public int? Cs { get; set; }

    public int? GoldEarned { get; set; }

    public int? DamageToChampions { get; set; }

    public int? VisionScore { get; set; }

    /// <summary>La note de performance (0 à 100), qui remplace la note calculée.</summary>
    public int? Note { get; set; }

    /// <summary>Le niveau atteint (1 à 18).</summary>
    public int? ChampLevel { get; set; }

    public int? Spell1Id { get; set; }

    public int? Spell2Id { get; set; }

    /// <summary>La plus longue série de kills.</summary>
    public int? LargestKillingSpree { get; set; }

    /// <summary>Le nombre de séries de kills.</summary>
    public int? KillingSprees { get; set; }

    public int? DoubleKills { get; set; }

    public int? TripleKills { get; set; }

    public int? QuadraKills { get; set; }

    public int? PentaKills { get; set; }

    /// <summary>Les sbires tués (précise le CS total).</summary>
    public int? MinionsKilled { get; set; }

    /// <summary>Les monstres neutres tués (précise le CS total).</summary>
    public int? NeutralMinionsKilled { get; set; }

    public int? GoldSpent { get; set; }

    public int? PhysicalDamageToChampions { get; set; }

    public int? MagicDamageToChampions { get; set; }

    public int? TrueDamageToChampions { get; set; }

    public int? DamageTaken { get; set; }

    public int? TotalHeal { get; set; }

    /// <summary>Les boucliers posés sur les alliés.</summary>
    public int? DamageShielded { get; set; }

    /// <summary>Le temps de contrôle infligé, en secondes.</summary>
    public int? TimeCcingOthers { get; set; }

    public int? WardsPlaced { get; set; }

    public int? WardsKilled { get; set; }

    public int? ControlWardsBought { get; set; }

    public int? DamageToObjectives { get; set; }

    public int? TurretKills { get; set; }

    public int? DragonKills { get; set; }

    public int? BaronKills { get; set; }

    public int? InhibitorKills { get; set; }

    /// <summary>L'objet de la quête de rôle.</summary>
    public int? RoleBoundItem { get; set; }

    /// <summary>L'arbre de runes principal.</summary>
    public int? PerkPrimaryStyle { get; set; }

    /// <summary>L'arbre de runes secondaire.</summary>
    public int? PerkSubStyle { get; set; }

    public int? Item0 { get; set; }

    public int? Item1 { get; set; }

    public int? Item2 { get; set; }

    public int? Item3 { get; set; }

    public int? Item4 { get; set; }

    public int? Item5 { get; set; }

    /// <summary>La babiole.</summary>
    public int? Item6 { get; set; }

    public int? Perk0 { get; set; }

    public int? Perk1 { get; set; }

    public int? Perk2 { get; set; }

    public int? Perk3 { get; set; }

    public int? Perk4 { get; set; }

    public int? Perk5 { get; set; }

    public DemandeDeCorrectionDeStats ToDemande()
    {
        var champs = new Dictionary<ChampCorrigeable, object>();
        if (Champion is { } champion) champs[ChampCorrigeable.Champion] = champion;
        if (Position is { } position) champs[ChampCorrigeable.Position] = position;
        if (Kills is { } kills) champs[ChampCorrigeable.Kills] = kills;
        if (Deaths is { } deaths) champs[ChampCorrigeable.Deaths] = deaths;
        if (Assists is { } assists) champs[ChampCorrigeable.Assists] = assists;
        if (Cs is { } cs) champs[ChampCorrigeable.Cs] = cs;
        if (GoldEarned is { } goldEarned) champs[ChampCorrigeable.GoldEarned] = goldEarned;
        if (DamageToChampions is { } damageToChampions) champs[ChampCorrigeable.DamageToChampions] = damageToChampions;
        if (VisionScore is { } visionScore) champs[ChampCorrigeable.VisionScore] = visionScore;
        if (Note is { } note) champs[ChampCorrigeable.Note] = note;
        if (ChampLevel is { } champLevel) champs[ChampCorrigeable.ChampLevel] = champLevel;
        if (Spell1Id is { } spell1Id) champs[ChampCorrigeable.Spell1Id] = spell1Id;
        if (Spell2Id is { } spell2Id) champs[ChampCorrigeable.Spell2Id] = spell2Id;
        if (LargestKillingSpree is { } largestKillingSpree) champs[ChampCorrigeable.LargestKillingSpree] = largestKillingSpree;
        if (KillingSprees is { } killingSprees) champs[ChampCorrigeable.KillingSprees] = killingSprees;
        if (DoubleKills is { } doubleKills) champs[ChampCorrigeable.DoubleKills] = doubleKills;
        if (TripleKills is { } tripleKills) champs[ChampCorrigeable.TripleKills] = tripleKills;
        if (QuadraKills is { } quadraKills) champs[ChampCorrigeable.QuadraKills] = quadraKills;
        if (PentaKills is { } pentaKills) champs[ChampCorrigeable.PentaKills] = pentaKills;
        if (MinionsKilled is { } minionsKilled) champs[ChampCorrigeable.MinionsKilled] = minionsKilled;
        if (NeutralMinionsKilled is { } neutralMinionsKilled) champs[ChampCorrigeable.NeutralMinionsKilled] = neutralMinionsKilled;
        if (GoldSpent is { } goldSpent) champs[ChampCorrigeable.GoldSpent] = goldSpent;
        if (PhysicalDamageToChampions is { } physicalDamageToChampions) champs[ChampCorrigeable.PhysicalDamageToChampions] = physicalDamageToChampions;
        if (MagicDamageToChampions is { } magicDamageToChampions) champs[ChampCorrigeable.MagicDamageToChampions] = magicDamageToChampions;
        if (TrueDamageToChampions is { } trueDamageToChampions) champs[ChampCorrigeable.TrueDamageToChampions] = trueDamageToChampions;
        if (DamageTaken is { } damageTaken) champs[ChampCorrigeable.DamageTaken] = damageTaken;
        if (TotalHeal is { } totalHeal) champs[ChampCorrigeable.TotalHeal] = totalHeal;
        if (DamageShielded is { } damageShielded) champs[ChampCorrigeable.DamageShielded] = damageShielded;
        if (TimeCcingOthers is { } timeCcingOthers) champs[ChampCorrigeable.TimeCcingOthers] = timeCcingOthers;
        if (WardsPlaced is { } wardsPlaced) champs[ChampCorrigeable.WardsPlaced] = wardsPlaced;
        if (WardsKilled is { } wardsKilled) champs[ChampCorrigeable.WardsKilled] = wardsKilled;
        if (ControlWardsBought is { } controlWardsBought) champs[ChampCorrigeable.ControlWardsBought] = controlWardsBought;
        if (DamageToObjectives is { } damageToObjectives) champs[ChampCorrigeable.DamageToObjectives] = damageToObjectives;
        if (TurretKills is { } turretKills) champs[ChampCorrigeable.TurretKills] = turretKills;
        if (DragonKills is { } dragonKills) champs[ChampCorrigeable.DragonKills] = dragonKills;
        if (BaronKills is { } baronKills) champs[ChampCorrigeable.BaronKills] = baronKills;
        if (InhibitorKills is { } inhibitorKills) champs[ChampCorrigeable.InhibitorKills] = inhibitorKills;
        if (RoleBoundItem is { } roleBoundItem) champs[ChampCorrigeable.RoleBoundItem] = roleBoundItem;
        if (PerkPrimaryStyle is { } perkPrimaryStyle) champs[ChampCorrigeable.PerkPrimaryStyle] = perkPrimaryStyle;
        if (PerkSubStyle is { } perkSubStyle) champs[ChampCorrigeable.PerkSubStyle] = perkSubStyle;
        if (Item0 is { } item0) champs[ChampCorrigeable.Item0] = item0;
        if (Item1 is { } item1) champs[ChampCorrigeable.Item1] = item1;
        if (Item2 is { } item2) champs[ChampCorrigeable.Item2] = item2;
        if (Item3 is { } item3) champs[ChampCorrigeable.Item3] = item3;
        if (Item4 is { } item4) champs[ChampCorrigeable.Item4] = item4;
        if (Item5 is { } item5) champs[ChampCorrigeable.Item5] = item5;
        if (Item6 is { } item6) champs[ChampCorrigeable.Item6] = item6;
        if (Perk0 is { } perk0) champs[ChampCorrigeable.Perk0] = perk0;
        if (Perk1 is { } perk1) champs[ChampCorrigeable.Perk1] = perk1;
        if (Perk2 is { } perk2) champs[ChampCorrigeable.Perk2] = perk2;
        if (Perk3 is { } perk3) champs[ChampCorrigeable.Perk3] = perk3;
        if (Perk4 is { } perk4) champs[ChampCorrigeable.Perk4] = perk4;
        if (Perk5 is { } perk5) champs[ChampCorrigeable.Perk5] = perk5;
        return new(champs);
    }
}
