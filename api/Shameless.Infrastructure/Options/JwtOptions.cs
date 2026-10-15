using System.ComponentModel.DataAnnotations;

namespace Shameless.Infrastructure.Options;

public sealed class JwtOptions
{
    [Required(ErrorMessage = "Auth:Jwt:Issuer missing")]
    public required string Issuer { get; init; }
    
    [Required(ErrorMessage = "Auth:Jwt:Audience missing")]
    public required string Audience { get; init; }
    
    [Required(ErrorMessage = "Auth:Jwt:SigningKey missing")]
    [MinLength(32, ErrorMessage = "Auth:Jwt:SigningKey must be at least 32 characters")]
    public required string SigningKey { get; init; }
    
    [Range(1, 1440)]
    public int AccessTokenMinutes { get; init; } = 60;
}