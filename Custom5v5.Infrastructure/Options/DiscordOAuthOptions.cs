using System.ComponentModel.DataAnnotations;

namespace Custom5v5.Infrastructure.Options;

public sealed class DiscordOAuthOptions
{
    [Required(ErrorMessage = "Auth:Discord:ClientId missing")]
    public required string ClientId { get; init; }
    
    [Required(ErrorMessage = "Auth:Discord:ClientSecret missing")]
    public required string ClientSecret { get; init; }
    
    [Required(ErrorMessage = "Auth:Discord:RedirectUri missing")]
    [Url]
    public required string RedirectUri { get; init; }
    
    public string BaseUrl { get; init; } = "https://discord.com/api/";
    public string? AllowedGuildId { get; init; }
    
    [Required]
    [Url]
    public required string FrontendUrl { get; init; }
}