namespace Shameless.Api.Contracts.Matches;

public class SetGameResultRequest
{
    public string Winner { get; set; } = string.Empty; // Équipe gagnante : "A" | "B"
}
