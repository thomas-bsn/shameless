namespace Custom5v5.Application.DTOs.Riot;
using System.Text.Json.Serialization;

public class SummonerResponse
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = null!;

    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = null!;

    [JsonPropertyName("puuid")]
    public string Puuid { get; set; } = null!;
}