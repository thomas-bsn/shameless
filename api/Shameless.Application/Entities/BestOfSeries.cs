namespace Shameless.Application.Entities;

public class BestOfSeries
{
    public int Id { get; set; }
    public DateTime PlayedAt { get; set; }
    public int Format { get; set; } // 1, 3, 5

    public int TeamAWins { get; set; } = 0;
    public int TeamBWins { get; set; } = 0;

    public ICollection<CustomGame> Games { get; set; } = new List<CustomGame>();
}
