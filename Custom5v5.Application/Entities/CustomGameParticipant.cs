namespace Custom5v5.Application.Entities;

public class CustomGameParticipant
{
    public int Id { get; set; }
    public int CustomGameId { get; set; }
    public int PlayerId { get; set; }
    public string Team { get; set; } = string.Empty; // "blue" | "red"
    public string? RiotParticipantData { get; set; }
    public bool Won { get; set; }
    
    // Navigation properties
    public CustomGame CustomGame { get; set; } = null!;
    public Player Player { get; set; } = null!;
}