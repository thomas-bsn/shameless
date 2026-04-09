namespace Custom5v5.Application.Entities;

public class Player
{
    public int Id { get; set; }
    public string PUUID { get; set; } = null!;  
    public string Prenom { get; set; } = null!;
    public string RiotId { get; set; } = null!;
    public string? RankTier { get; set; }
    public int? RankDivision { get; set; }
    public string? MainRole { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? LP { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }
    public string? PeakTier { get; set; }
    public int? PeakDivision { get; set; }
    public string? PeakSeason { get; set; }
    public int PeakLp { get; set; } = 0;
    
    public decimal CurrentCost { get; set; }
    public bool IsPlacement { get; set; }
    
    public ICollection<CustomGameParticipant> CustomGameParticipations { get; set; } = new List<CustomGameParticipant>();
    public ICollection<PlayerMonthlyCost> MonthlyCosts { get; set; } = new List<PlayerMonthlyCost>();
}