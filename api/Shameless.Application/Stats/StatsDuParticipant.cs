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
    Role? Position)
{
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
            Position: RoleDe(champs.Texte("teamPosition", "individualPosition", "lane", "TEAM_POSITION")));
    }

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
