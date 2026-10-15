using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Custom5v5.Api.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Custom5v5.Api.Services;

public sealed class JwtIssuer : IJwtIssuer
{
    private readonly string _issuer;
    private readonly string _audience;
    private readonly SymmetricSecurityKey _signingKey;
    private readonly int _expiresMinutes;

    public JwtIssuer(IConfiguration config)
    {
        _issuer = config["Auth:Jwt:Issuer"] ?? throw new InvalidOperationException("Missing Auth:Jwt:Issuer");
        _audience = config["Auth:Jwt:Audience"] ?? throw new InvalidOperationException("Missing Auth:Jwt:Audience");

        var key = config["Auth:Jwt:SigningKey"];
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            throw new InvalidOperationException("Missing/weak Auth:Jwt:SigningKey (use 32+ chars random secret).");

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));

        _expiresMinutes = config.GetValue<int?>("Auth:Jwt:ExpiresMinutes") ?? 360;
        _expiresMinutes = Math.Clamp(_expiresMinutes, 5, 60 * 24 * 7); // 5 min .. 7 days max
    }

    public JwtIssueResult Issue(JwtUser user)
    {
        if (string.IsNullOrWhiteSpace(user.DiscordUserId))
            throw new ArgumentException("DiscordUserId is required.", nameof(user));

        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_expiresMinutes);

        var claims = new List<Claim>
        {
            // Standard JWT subject
            new(JwtRegisteredClaimNames.Sub, user.DiscordUserId),

            // Make ASP.NET Core happy with ClaimTypes.NameIdentifier too
            new(ClaimTypes.NameIdentifier, user.DiscordUserId),

            // Optional username
            new(ClaimTypes.Name, user.Username ?? string.Empty),

            // Token id (useful later if you want revocation / blacklist)
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),

            new(JwtRegisteredClaimNames.Iat, ToUnixTimeSeconds(now).ToString(), ClaimValueTypes.Integer64),
        };
        
        if (!string.IsNullOrWhiteSpace(user.AvatarUrl))
            claims.Add(new Claim("avatar_url", user.AvatarUrl));

        var creds = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _issuer,
            audience: _audience,
            claims: claims,
            notBefore: now,
            expires: expires,
            signingCredentials: creds
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);
        return new JwtIssueResult(accessToken, expires);
    }

    private static long ToUnixTimeSeconds(DateTime utc)
        => new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeSeconds();
}