namespace Shameless.Api.Contracts.Matches;

/// <summary>
/// La correction d'une game terminée : le vainqueur et/ou la composition. La composition se donne pour
/// les deux Équipes à la fois (cinq joueurs chacune).
/// </summary>
public class CorrectGameRequest
{
    /// <summary>Le nouveau vainqueur, « A » ou « B » ; absent pour le laisser tel quel.</summary>
    public string? Winner { get; set; }

    /// <summary>Les ids des cinq joueurs de l'Équipe A ; absent pour laisser la composition telle quelle.</summary>
    public List<int>? TeamA { get; set; }

    /// <summary>Les ids des cinq joueurs de l'Équipe B ; absent pour laisser la composition telle quelle.</summary>
    public List<int>? TeamB { get; set; }
}
