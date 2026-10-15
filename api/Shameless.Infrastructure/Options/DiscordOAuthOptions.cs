using System.ComponentModel.DataAnnotations;

namespace Shameless.Infrastructure.Options;

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

    [Required(ErrorMessage = "Auth:Discord:AllowedGuildId missing")]
    public required string AllowedGuildId { get; init; }
}