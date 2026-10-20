namespace Shameless.Application.Stats;

/// <summary>
/// Les sorts lancés par un joueur : les quatre sorts du champion (Q, W, E, R) et les deux sorts
/// d'invocateur (D, F). Chaque compteur est null quand le Rapport ne le porte pas.
/// </summary>
public sealed record SortsLances(int? Q, int? W, int? E, int? R, int? D, int? F)
{
    internal static SortsLances? Lire(ChampsLcu champs)
    {
        var sorts = new SortsLances(
            champs.Entier("spell1Casts"), champs.Entier("spell2Casts"),
            champs.Entier("spell3Casts"), champs.Entier("spell4Casts"),
            champs.Entier("summoner1Casts"), champs.Entier("summoner2Casts"));
        return sorts == new SortsLances(null, null, null, null, null, null) ? null : sorts;
    }
}

/// <summary>Les pings d'un joueur par type. Chaque compteur est null quand le Rapport ne le porte pas.</summary>
public sealed record PingsDuJoueur(
    int? AllIn, int? AssistMe, int? Basic, int? Command, int? Danger, int? EnemyMissing, int? EnemyVision,
    int? GetBack, int? Hold, int? NeedVision, int? OnMyWay, int? Push, int? Retreat, int? VisionCleared)
{
    internal static PingsDuJoueur? Lire(ChampsLcu champs)
    {
        var pings = new PingsDuJoueur(
            champs.Entier("allInPings"), champs.Entier("assistMePings"), champs.Entier("basicPings"),
            champs.Entier("commandPings"), champs.Entier("dangerPings"), champs.Entier("enemyMissingPings"),
            champs.Entier("enemyVisionPings"), champs.Entier("getBackPings"), champs.Entier("holdPings"),
            champs.Entier("needVisionPings"), champs.Entier("onMyWayPings"), champs.Entier("pushPings"),
            champs.Entier("retreatPings"), champs.Entier("visionClearedPings"));
        return pings == new PingsDuJoueur(null, null, null, null, null, null, null, null, null, null, null, null, null, null)
            ? null : pings;
    }
}
