namespace Custom5v5.Api.Contracts.Matches;

public class RecordGameRequest
{
    public List<int> BlueTeam { get; set; } = [];
    public List<int> RedTeam { get; set; } = [];
}