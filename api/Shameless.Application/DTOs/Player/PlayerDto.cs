namespace Shameless.Application.DTOs.Players;

public class PlayerDto
{
    public int Id { get; set; }
    public required string Prenom { get; set; }
    public required string RiotId { get; set; }
    public required string? Puuid { get; set; }
    public string? RankTier { get; set; }
    public int? RankDivision { get; set; }
    public string? MainRole { get; set; }
    public int? Lp { get; set; }
    public string? PeakTier { get; set; }
    public int? PeakDivision { get; set; }
    public string? PeakSeason { get; set; }
    public int PeakLp { get; set; } = 0;
}