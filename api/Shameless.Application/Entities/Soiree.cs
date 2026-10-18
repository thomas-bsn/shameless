namespace Shameless.Application.Entities;

/// <summary>Une session de jeu qui regroupe des séries. Au plus une Soirée ouverte par instance.</summary>
public class Soiree
{
    public int Id { get; set; }
    public SoireeStatus Status { get; set; } = SoireeStatus.Open;
    public DateTime OpenedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool FermetureAutomatique { get; set; }

    public ICollection<BestOfSeries> Series { get; set; } = new List<BestOfSeries>();

    /// <summary>La session de Picker en cours, s'il y en a une.</summary>
    public PickerSession? Picker { get; set; }
}

public enum SoireeStatus
{
    Open,
    Closed
}
