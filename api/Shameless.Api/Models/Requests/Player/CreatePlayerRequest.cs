namespace Shameless.Application.DTOs.Players;

public class CreatePlayerRequest
{
    public string Prenom { get; set; } = null!;
    public string RiotId { get; set; } = null!;
}