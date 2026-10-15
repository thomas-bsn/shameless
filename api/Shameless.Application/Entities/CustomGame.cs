namespace Shameless.Application.Entities;

public class CustomGame
{
    public int Id { get; set; }
    public DateTime PlayedAt { get; set; }
    public string TeamASide { get; set; } = Sides.Blue; // Side joué par l'Équipe A : "blue" | "red"
    public string? WinningTeam { get; set; } // Équipe gagnante : "A" | "B", null tant que la game n'est pas terminée
    public string? RiotMatchId { get; set; } // NULL pour maintenant, rempli plus tard
    public string? RiotMatchData { get; set; }
    public string Status { get; set; } = GameStatuses.Pending; // voir GameStatuses
    public DateTime CreatedAt { get; set; }
    public int? SeriesId { get; set; }
    public BestOfSeries? Series { get; set; }


    // Navigation properties
    public ICollection<CustomGameParticipant> Participants { get; set; } = new List<CustomGameParticipant>();

    public string TeamBSide => Sides.Other(TeamASide);

    /// <summary>L'Équipe qui joue le Side donné dans cette game.</summary>
    public string EquipeOn(string side) => side == TeamASide ? Equipes.A : Equipes.B;

    /// <summary>La composition d'une Équipe pour cette game.</summary>
    public IEnumerable<CustomGameParticipant> ParticipantsOf(string equipe) =>
        Participants.Where(p => EquipeOn(p.Team) == equipe);
}
