using Shameless.Application.Picker;

namespace Shameless.Application.Stats;

/// <summary>
/// Les stats d'un Joueur pour une Game, lues dans ses stats brutes envoyées par le Companion
/// (<c>CustomGameParticipant.RiotParticipantData</c>, conservées telles quelles : elles restent la
/// source de vérité et peuvent être relues si la lecture s'enrichit). Chaque stat est null quand la
/// source ne la fournit pas : les calculs (KDA, CS/min, KP, note…) l'omettent alors.
/// </summary>
/// <remarks>
/// Noms attendus dans le JSON du Companion, du premier choisi au dernier (voir <see cref="Lire"/>) :
/// camelCase de l'historique de match LCU / match-v5 (ex. <c>totalMinionsKilled</c>), puis UPPER_SNAKE
/// du bloc de fin de game LCU (ex. <c>MINIONS_KILLED</c>) ; à la racine puis sous <c>stats</c> ou
/// <c>timeline</c>.
/// </remarks>
/// <param name="MinionsKilled">Sbires tués (<c>totalMinionsKilled</c>) ; le CS est sa somme avec <paramref name="NeutralMinionsKilled"/>.</param>
/// <param name="NeutralMinionsKilled">Monstres neutres tués (<c>neutralMinionsKilled</c>).</param>
/// <param name="DamageToChampions">Dégâts infligés aux champions (<c>totalDamageDealtToChampions</c>).</param>
/// <param name="DamageTaken">Dégâts subis (<c>totalDamageTaken</c>).</param>
/// <param name="ControlWardsBought">Balises de contrôle achetées (<c>visionWardsBoughtInGame</c>).</param>
/// <param name="DamageToObjectives">Dégâts aux objectifs (<c>damageDealtToObjectives</c>).</param>
/// <param name="TurretKills">Tourelles détruites (<c>turretKills</c>).</param>
/// <param name="DragonKills">Dragons tués (<c>dragonKills</c>).</param>
/// <param name="BaronKills">Barons tués (<c>baronKills</c>).</param>
/// <param name="ChampLevel">Niveau atteint (<c>champLevel</c>).</param>
/// <param name="Position">Le rôle joué, lu dans <c>teamPosition</c>, <c>individualPosition</c> ou <c>lane</c> ; null si inconnu.</param>
/// <param name="Spell1Id">Premier sort d'invocateur (<c>spell1Id</c>, identifiant du client).</param>
/// <param name="Spell2Id">Second sort d'invocateur (<c>spell2Id</c>).</param>
/// <param name="LargestKillingSpree">Plus longue série de kills sans mourir.</param>
/// <param name="KillingSprees">Nombre de séries de kills.</param>
/// <param name="GoldSpent">Or dépensé (<c>goldSpent</c>).</param>
/// <param name="TotalHeal">Soins (<c>totalHeal</c>).</param>
/// <param name="DamageShielded">Boucliers posés sur les alliés (<c>totalDamageShieldedOnTeammates</c>).</param>
/// <param name="TimeCcingOthers">Temps de contrôle infligé en secondes (<c>timeCCingOthers</c>).</param>
/// <param name="RoleBoundItem">L'objet de la quête de rôle (<c>roleBoundItem</c>, identifiant du client).</param>
/// <param name="PerkPrimaryStyle">L'arbre de runes principal (identifiant du client).</param>
/// <param name="PerkSubStyle">L'arbre de runes secondaire.</param>
/// <param name="Items">Les sept emplacements d'objets (six objets et la babiole), 0 ou null pour un emplacement vide ; null sans objets.</param>
/// <param name="Perks">Les six runes (<c>PERK0</c> à <c>PERK5</c>) ; null sans runes. Les fragments ne sont dans aucune donnée du client.</param>
/// <param name="SortsLances">Les sorts lancés (<c>spell1Casts</c> à <c>spell4Casts</c>, <c>summoner1Casts</c>, <c>summoner2Casts</c>) ; null sans aucun compteur.</param>
/// <param name="Pings">Les pings par type (<c>*Pings</c>) ; null sans aucun compteur.</param>
public sealed record StatsDuParticipant(
    int? Kills,
    int? Deaths,
    int? Assists,
    int? MinionsKilled,
    int? NeutralMinionsKilled,
    int? DamageToChampions,
    int? DamageTaken,
    int? GoldEarned,
    int? VisionScore,
    int? WardsPlaced,
    int? WardsKilled,
    int? ControlWardsBought,
    int? DamageToObjectives,
    int? TurretKills,
    int? DragonKills,
    int? BaronKills,
    int? ChampLevel,
    Role? Position,
    int? Spell1Id = null,
    int? Spell2Id = null,
    int? LargestKillingSpree = null,
    int? KillingSprees = null,
    int? DoubleKills = null,
    int? TripleKills = null,
    int? QuadraKills = null,
    int? PentaKills = null,
    int? GoldSpent = null,
    int? PhysicalDamageToChampions = null,
    int? MagicDamageToChampions = null,
    int? TrueDamageToChampions = null,
    int? TotalHeal = null,
    int? DamageShielded = null,
    int? TimeCcingOthers = null,
    int? InhibitorKills = null,
    int? RoleBoundItem = null,
    int? PerkPrimaryStyle = null,
    int? PerkSubStyle = null,
    IReadOnlyList<int?>? Items = null,
    IReadOnlyList<int?>? Perks = null,
    SortsLances? SortsLances = null,
    PingsDuJoueur? Pings = null)
{
    public const int NombreDObjets = 7;
    public const int NombreDeRunes = 6;

    /// <summary>Les stats lues dans le JSON brut du participant, null sans stats (saisie manuelle, absent du rapport).</summary>
    public static StatsDuParticipant? Lire(string? riotParticipantData)
    {
        if (ChampsLcu.De(riotParticipantData, "stats", "timeline") is not { } champs) return null;
        return new StatsDuParticipant(
            Kills: champs.Entier("kills", "CHAMPIONS_KILLED"),
            Deaths: champs.Entier("deaths", "NUM_DEATHS"),
            Assists: champs.Entier("assists", "ASSISTS"),
            MinionsKilled: champs.Entier("totalMinionsKilled", "minionsKilled", "MINIONS_KILLED"),
            NeutralMinionsKilled: champs.Entier("neutralMinionsKilled", "NEUTRAL_MINIONS_KILLED"),
            DamageToChampions: champs.Entier("totalDamageDealtToChampions", "TOTAL_DAMAGE_DEALT_TO_CHAMPIONS"),
            DamageTaken: champs.Entier("totalDamageTaken", "TOTAL_DAMAGE_TAKEN"),
            GoldEarned: champs.Entier("goldEarned", "GOLD_EARNED"),
            VisionScore: champs.Entier("visionScore", "VISION_SCORE"),
            WardsPlaced: champs.Entier("wardsPlaced", "WARD_PLACED"),
            WardsKilled: champs.Entier("wardsKilled", "WARD_KILLED"),
            ControlWardsBought: champs.Entier("visionWardsBoughtInGame", "VISION_WARDS_BOUGHT_IN_GAME"),
            DamageToObjectives: champs.Entier("damageDealtToObjectives", "TOTAL_DAMAGE_DEALT_TO_OBJECTIVES"),
            TurretKills: champs.Entier("turretKills", "TURRETS_KILLED"),
            DragonKills: champs.Entier("dragonKills", "DRAGON_KILLS"),
            BaronKills: champs.Entier("baronKills", "BARON_KILLS"),
            ChampLevel: champs.Entier("champLevel", "LEVEL"),
            Position: RoleDe(champs.Texte("teamPosition", "individualPosition", "lane", "TEAM_POSITION")),
            Spell1Id: champs.Entier("spell1Id"),
            Spell2Id: champs.Entier("spell2Id"),
            LargestKillingSpree: champs.Entier("largestKillingSpree", "LARGEST_KILLING_SPREE"),
            KillingSprees: champs.Entier("killingSprees", "KILLING_SPREES"),
            DoubleKills: champs.Entier("doubleKills", "DOUBLE_KILLS"),
            TripleKills: champs.Entier("tripleKills", "TRIPLE_KILLS"),
            QuadraKills: champs.Entier("quadraKills", "QUADRA_KILLS"),
            PentaKills: champs.Entier("pentaKills", "PENTA_KILLS"),
            GoldSpent: champs.Entier("goldSpent", "GOLD_SPENT"),
            PhysicalDamageToChampions: champs.Entier("physicalDamageDealtToChampions", "PHYSICAL_DAMAGE_DEALT_TO_CHAMPIONS"),
            MagicDamageToChampions: champs.Entier("magicDamageDealtToChampions", "MAGIC_DAMAGE_DEALT_TO_CHAMPIONS"),
            TrueDamageToChampions: champs.Entier("trueDamageDealtToChampions", "TRUE_DAMAGE_DEALT_TO_CHAMPIONS"),
            TotalHeal: champs.Entier("totalHeal", "TOTAL_HEAL"),
            DamageShielded: champs.Entier("totalDamageShieldedOnTeammates", "TOTAL_DAMAGE_SHIELDED_ON_TEAMMATES"),
            TimeCcingOthers: champs.Entier("timeCCingOthers", "TIME_CCING_OTHERS"),
            InhibitorKills: champs.Entier("inhibitorKills", "BARRACKS_KILLED"),
            RoleBoundItem: champs.Entier("roleBoundItem", "ROLE_BOUND_ITEM"),
            PerkPrimaryStyle: champs.Entier("perkPrimaryStyle", "PERK_PRIMARY_STYLE"),
            PerkSubStyle: champs.Entier("perkSubStyle", "PERK_SUB_STYLE"),
            Items: champs.Entiers("items") is { } items ? Completer(items, NombreDObjets) : null,
            Perks: Enumerable.Range(0, NombreDeRunes).Select(i => champs.Entier($"PERK{i}", $"perk{i}")).ToArray() is { } perks
                   && perks.Any(p => p is not null) ? perks : null,
            SortsLances: SortsLances.Lire(champs),
            Pings: PingsDuJoueur.Lire(champs));
    }

    /// <summary>Les <paramref name="nombre"/> premières cases, complétées par des emplacements vides.</summary>
    internal static int?[] Completer(IReadOnlyList<int?> cases, int nombre) =>
        Enumerable.Range(0, nombre).Select(i => i < cases.Count ? cases[i] : null).ToArray();

    /// <summary>Le rôle Shameless d'une position Riot (TOP, JUNGLE, MIDDLE, BOTTOM, UTILITY, ou leurs variantes).</summary>
    private static Role? RoleDe(string? position) => position?.ToUpperInvariant() switch
    {
        "TOP" => Role.Top,
        "JUNGLE" => Role.Jungle,
        "MIDDLE" or "MID" => Role.Mid,
        "BOTTOM" or "BOT" or "ADC" or "CARRY" => Role.Adc,
        "UTILITY" or "SUPPORT" => Role.Support,
        _ => null
    };
}
