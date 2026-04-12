namespace Custom5v5.Application.Entities;
public class BestOfSeries
{
    public int Id { get; set; }
    public DateTime PlayedAt { get; set; }
    public int Format { get; set; } // 1, 3, 5
    public ICollection<CustomGame> Games { get; set; } = new List<CustomGame>();
}