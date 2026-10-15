namespace Shameless.Application.Entities;

public class CustomGameParticipant
{
    public int Id { get; set; }
    public int CustomGameId { get; set; }
    public int PlayerId { get; set; }

    /// <summary>Le Side joué par le participant dans cette game : "blue" | "red".</summary>
    public string Side { get; set; } = string.Empty;

    public string? RiotParticipantData { get; set; }

    // Navigation properties
    public CustomGame CustomGame { get; set; } = null!;
    public Player Player { get; set; } = null!;

    /// <summary>L'Équipe du participant a gagné cette game.</summary>
    public bool HasWon => CustomGame.WinningTeam != null && CustomGame.EquipeOn(Side) == CustomGame.WinningTeam;
}
