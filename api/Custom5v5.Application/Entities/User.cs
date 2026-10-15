namespace Custom5v5.Application.Entities;

public class User
{
    public int Id { get; set; }
    public long DiscordId { get; set; }
    public string? DiscordUsername { get; set; }
    public string? AvatarUrl { get; set; }
    public Player? Player { get; set; }
}