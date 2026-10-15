namespace Shameless.Application.Entities;

public class CustomGame
{
    public int Id { get; set; }
    public DateTime PlayedAt { get; set; }
    public string? Winner { get; set; } = string.Empty; // "blue" | "red"
    public string? RiotMatchId { get; set; } // NULL pour maintenant, rempli plus tard
    public string? RiotMatchData { get; set; }
    public string Status { get; set; } = "pending"; // "pending", "completed", "error"
    public DateTime CreatedAt { get; set; }
    public int? SeriesId { get; set; }
    public BestOfSeries? Series { get; set; }
    
    
    // Navigation properties
    public ICollection<CustomGameParticipant> Participants { get; set; } = new List<CustomGameParticipant>();
}