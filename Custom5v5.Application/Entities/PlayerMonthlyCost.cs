namespace Custom5v5.Application.Entities;

public class PlayerMonthlyCost
{
    public int Id { get; set; }
    public int PlayerId { get; set; }
    public DateTime Month { get; set; } // 2025-01-01, 2025-02-01, etc.
    public decimal FinalCost { get; set; }
    public int BaseMMR { get; set; }
    public int WinrateAdjustment { get; set; }
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }
    
    // Navigation property
    public Player Player { get; set; } = null!;
}