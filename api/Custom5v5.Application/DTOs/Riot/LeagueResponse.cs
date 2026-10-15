namespace Custom5v5.Application.DTOs.Riot;

public class LeagueResponse
{
    public string QueueType { get; set; } = null!;
    public string Tier { get; set; } = null!;
    public string Rank { get; set; } = null!;
    public int LeaguePoints { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
}