using Shameless.Application.Services;

namespace Shameless.Api.Contracts.Matches;

/// <summary>
/// La correction des stats d'un joueur dans une game terminée. Chaque champ absent reste tel quel ; une
/// correction remplace la valeur du Companion à l'affichage, sans la modifier.
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

    public DemandeDeCorrectionDeStats ToDemande() =>
        new(Champion, Position, Kills, Deaths, Assists, Cs, GoldEarned, DamageToChampions, VisionScore, Note);
}
