using Shameless.Application.Picker;

namespace Shameless.Application.Entities;

/// <summary>Un joueur de la composition courante d'une Équipe dans une Série.</summary>
public class SeriesPlayer
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public int PlayerId { get; set; }
    public string Equipe { get; set; } = Equipes.A; // "A" | "B"

    /// <summary>Le rôle attribué par l'Autorole du Picker, null sans Autorole.</summary>
    public Role? Role { get; set; }

    /// <summary>
    /// Le joueur est le Capitaine de son Équipe : il mène sa Draft de champions. Au plus un par Équipe ;
    /// repris de la Draft du Picker, sinon désigné par l'organisateur.
    /// </summary>
    public bool Capitaine { get; set; }

    public BestOfSeries Series { get; set; } = null!;
    public Player Player { get; set; } = null!;
}
