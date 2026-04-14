namespace Custom5v5.Api.Contracts.Matches;

public class SetGameResultRequest
{
    public string Winner { get; set; } = string.Empty; // "blue" | "red"
}