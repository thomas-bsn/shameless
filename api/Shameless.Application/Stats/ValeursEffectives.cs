using System.Globalization;
using Shameless.Application.Entities;
using Shameless.Application.Picker;

namespace Shameless.Application.Stats;

/// <summary>
/// La lecture unique des valeurs d'un participant : les données brutes du Companion, remplacées champ
/// par champ par les corrections d'un Administrateur quand elles existent. Tout ce qui affiche ou
/// calcule à partir des stats d'un participant (page de la game, fiche, note de performance, cost,
/// bot) passe par ici, jamais par <c>RiotParticipantData</c> directement.
/// </summary>
public static class ValeursEffectives
{
    /// <summary>
    /// Les stats du participant, corrigées ; null sans stats du Companion et sans correction de stat.
    /// Un joueur sans rapport dont un admin corrige une stat n'a que cette stat.
    /// </summary>
    public static StatsDuParticipant? Stats(CustomGameParticipant participant)
    {
        var brutes = StatsDuParticipant.Lire(participant.RiotParticipantData);
        var corrections = participant.Corrections;
        if (!corrections.Any(c => c.Champ is not (ChampCorrigeable.Champion or ChampCorrigeable.Note)))
            return brutes;

        var s = brutes ?? new StatsDuParticipant(null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null, null);
        foreach (var c in corrections.Where(c => c.Champ != ChampCorrigeable.Cs))
            s = Appliquer(s, c);
        if (corrections.FirstOrDefault(c => c.Champ == ChampCorrigeable.Cs) is { } cs)
            s = AppliquerLeTotalDeCs(s, Entier(cs),
                corrections.Any(c => c.Champ == ChampCorrigeable.MinionsKilled),
                corrections.Any(c => c.Champ == ChampCorrigeable.NeutralMinionsKilled));
        return s;
    }

    /// <summary>Les stats telles que le Companion les a envoyées, sans correction ; null sans stats du Companion.</summary>
    public static StatsDuParticipant? StatsOrigine(CustomGameParticipant participant) =>
        StatsDuParticipant.Lire(participant.RiotParticipantData);

    /// <summary>Le champion du rapport du Companion, sans correction.</summary>
    public static string? ChampionOrigine(CustomGameParticipant participant) => participant.ChampionId;

    /// <summary>
    /// Le CS vaut sbires + monstres neutres : le total corrigé complète la moitié corrigée (plancher 0).
    /// Sans moitié corrigée, il remplace les sbires et met les monstres à 0.
    /// </summary>
    private static StatsDuParticipant AppliquerLeTotalDeCs(StatsDuParticipant s, int total, bool sbiresCorriges, bool monstresCorriges) =>
        (sbiresCorriges, monstresCorriges) switch
        {
            (true, true) => s,
            (true, false) => s with { NeutralMinionsKilled = Math.Max(0, total - (s.MinionsKilled ?? 0)) },
            (false, true) => s with { MinionsKilled = Math.Max(0, total - (s.NeutralMinionsKilled ?? 0)) },
            _ => s with { MinionsKilled = total, NeutralMinionsKilled = 0 }
        };

    private static StatsDuParticipant Appliquer(StatsDuParticipant s, CorrectionDeStat c)
    {
        if (c.Champ == ChampCorrigeable.Position) return s with { Position = Enum.Parse<Role>(c.Valeur) };
        if (c.Champ is ChampCorrigeable.Champion or ChampCorrigeable.Note) return s;
        var v = Entier(c);
        if (c.Champ is >= ChampCorrigeable.Item0 and <= ChampCorrigeable.Item6)
            return s with { Items = Remplacer(s.Items, StatsDuParticipant.NombreDObjets, c.Champ - ChampCorrigeable.Item0, v) };
        if (c.Champ >= ChampCorrigeable.Perk0)
            return s with { Perks = Remplacer(s.Perks, StatsDuParticipant.NombreDeRunes, c.Champ - ChampCorrigeable.Perk0, v) };
        return c.Champ switch
        {
            ChampCorrigeable.Kills => s with { Kills = v },
            ChampCorrigeable.Deaths => s with { Deaths = v },
            ChampCorrigeable.Assists => s with { Assists = v },
            ChampCorrigeable.Cs => s, // appliqué à part, après les autres : AppliquerLeTotalDeCs
            ChampCorrigeable.GoldEarned => s with { GoldEarned = v },
            ChampCorrigeable.DamageToChampions => s with { DamageToChampions = v },
            ChampCorrigeable.VisionScore => s with { VisionScore = v },
            ChampCorrigeable.ChampLevel => s with { ChampLevel = v },
            ChampCorrigeable.Spell1Id => s with { Spell1Id = v },
            ChampCorrigeable.Spell2Id => s with { Spell2Id = v },
            ChampCorrigeable.LargestKillingSpree => s with { LargestKillingSpree = v },
            ChampCorrigeable.KillingSprees => s with { KillingSprees = v },
            ChampCorrigeable.DoubleKills => s with { DoubleKills = v },
            ChampCorrigeable.TripleKills => s with { TripleKills = v },
            ChampCorrigeable.QuadraKills => s with { QuadraKills = v },
            ChampCorrigeable.PentaKills => s with { PentaKills = v },
            ChampCorrigeable.MinionsKilled => s with { MinionsKilled = v },
            ChampCorrigeable.NeutralMinionsKilled => s with { NeutralMinionsKilled = v },
            ChampCorrigeable.GoldSpent => s with { GoldSpent = v },
            ChampCorrigeable.PhysicalDamageToChampions => s with { PhysicalDamageToChampions = v },
            ChampCorrigeable.MagicDamageToChampions => s with { MagicDamageToChampions = v },
            ChampCorrigeable.TrueDamageToChampions => s with { TrueDamageToChampions = v },
            ChampCorrigeable.DamageTaken => s with { DamageTaken = v },
            ChampCorrigeable.TotalHeal => s with { TotalHeal = v },
            ChampCorrigeable.DamageShielded => s with { DamageShielded = v },
            ChampCorrigeable.TimeCcingOthers => s with { TimeCcingOthers = v },
            ChampCorrigeable.WardsPlaced => s with { WardsPlaced = v },
            ChampCorrigeable.WardsKilled => s with { WardsKilled = v },
            ChampCorrigeable.ControlWardsBought => s with { ControlWardsBought = v },
            ChampCorrigeable.DamageToObjectives => s with { DamageToObjectives = v },
            ChampCorrigeable.TurretKills => s with { TurretKills = v },
            ChampCorrigeable.DragonKills => s with { DragonKills = v },
            ChampCorrigeable.BaronKills => s with { BaronKills = v },
            ChampCorrigeable.InhibitorKills => s with { InhibitorKills = v },
            ChampCorrigeable.RoleBoundItem => s with { RoleBoundItem = v },
            ChampCorrigeable.PerkPrimaryStyle => s with { PerkPrimaryStyle = v },
            ChampCorrigeable.PerkSubStyle => s with { PerkSubStyle = v },
            _ => s
        };
    }

    private static int?[] Remplacer(IReadOnlyList<int?>? cases, int nombre, int indice, int valeur)
    {
        var copie = StatsDuParticipant.Completer(cases ?? [], nombre);
        copie[indice] = valeur;
        return copie;
    }

    /// <summary>Le champion joué : celui corrigé, sinon celui du rapport du Companion.</summary>
    public static string? Champion(CustomGameParticipant participant) =>
        participant.Corrections.FirstOrDefault(c => c.Champ == ChampCorrigeable.Champion)?.Valeur
        ?? participant.ChampionId;

    /// <summary>La note de performance imposée par un admin, null si elle est calculée.</summary>
    public static int? NoteCorrigee(CustomGameParticipant participant) =>
        participant.Corrections.FirstOrDefault(c => c.Champ == ChampCorrigeable.Note) is { } c ? Entier(c) : null;

    private static int Entier(CorrectionDeStat c) => int.Parse(c.Valeur, CultureInfo.InvariantCulture);
}
