using System.ComponentModel.DataAnnotations;

namespace Shameless.Infrastructure.Options;

public sealed class RiotApiOptions
{
    [Required(ErrorMessage = "Auth:Riot:ApiKey missing")]
    public required string ApiKey { get; init; }
    
    [Required]
    [Url]
    public string BaseUrl { get; init; } = "https://europe.api.riotgames.com";
    
    [Required]
    [Url]
    public string RegionalBaseUrl { get; init; } = "https://euw1.api.riotgames.com";
    
    [Range(1, 300)]
    public int TimeoutSeconds { get; init; } = 30;
    
    [Range(0, 5000)]
    public int RateLimitDelayMs { get; init; } = 1200;
}