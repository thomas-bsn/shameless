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
        foreach (var c in corrections)
        {
            s = c.Champ switch
            {
                ChampCorrigeable.Kills => s with { Kills = Entier(c) },
                ChampCorrigeable.Deaths => s with { Deaths = Entier(c) },
                ChampCorrigeable.Assists => s with { Assists = Entier(c) },
                // Le CS vaut sbires + monstres neutres : la correction donne le total.
                ChampCorrigeable.Cs => s with { MinionsKilled = Entier(c), NeutralMinionsKilled = 0 },
                ChampCorrigeable.GoldEarned => s with { GoldEarned = Entier(c) },
                ChampCorrigeable.DamageToChampions => s with { DamageToChampions = Entier(c) },
                ChampCorrigeable.VisionScore => s with { VisionScore = Entier(c) },
                ChampCorrigeable.Position => s with { Position = Enum.Parse<Role>(c.Valeur) },
                _ => s
            };
        }
        return s;
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
