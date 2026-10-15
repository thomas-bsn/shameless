namespace Shameless.Application.Entities;

/// <summary>Un joueur de la composition courante d'une Équipe dans une Série.</summary>
public class SeriesPlayer
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public int PlayerId { get; set; }
    public string Equipe { get; set; } = Equipes.A; // "A" | "B"

    public BestOfSeries Series { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
