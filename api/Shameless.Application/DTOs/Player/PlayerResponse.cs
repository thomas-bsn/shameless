namespace Shameless.Application.DTOs.Players;

public class PlayerResponse
{
    public int Id { get; set; }
    public required string Prenom { get; set; }
    public required string RiotId { get; set; }
    public string? RankTier { get; set; }
    public int? RankDivision { get; set; }
    public int? Lp { get; set; }
}