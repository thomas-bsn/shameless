using System.ComponentModel.DataAnnotations;

namespace Custom5v5.Infrastructure.Options;

public sealed class DatabaseOptions
{
    [Required(ErrorMessage = "Database:Host missing")]
    public required string Host { get; init; }
    
    [Range(1, 65535, ErrorMessage = "Database:Port must be between 1 and 65535")]
    public int Port { get; init; } = 5432;
    
    [Required(ErrorMessage = "Database:Database missing")]
    public required string Database { get; init; }
    
    [Required(ErrorMessage = "Database:Username missing")]
    public required string Username { get; init; }
    
    [Required(ErrorMessage = "Database:Password missing")]
    public required string Password { get; init; }
}