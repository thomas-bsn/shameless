using System.Security.Claims;
using Custom5v5.Api.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace Custom5v5.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IOAuthStateStore _stateStore;
    private readonly IDiscordOAuthClient _discord;
    private readonly IJwtIssuer _jwt;

    public AuthController(
        IConfiguration config,
        IOAuthStateStore stateStore,
        IDiscordOAuthClient discord,
        IJwtIssuer jwt)
    {
        _config = config;
        _stateStore = stateStore;
        _discord = discord;
        _jwt = jwt;
    }

    [HttpGet("discord/login")]
    [AllowAnonymous]
    public IActionResult DiscordLogin([FromQuery] string? returnUrl = null)
    {
        var clientId = _config["Auth:Discord:ClientId"];
        var redirectUri = _config["Auth:Discord:RedirectUri"];

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(redirectUri))
            return Problem("Missing Auth:Discord:ClientId or Auth:Discord:RedirectUri in configuration.");

        var state = _stateStore.CreateState(returnUrl);
        var scope = "identify guilds";

        var authorizeUrl =
            "https://discord.com/api/oauth2/authorize" +
            $"?client_id={Uri.EscapeDataString(clientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(redirectUri)}" +
            "&response_type=code" +
            $"&scope={Uri.EscapeDataString(scope)}" +
            $"&state={Uri.EscapeDataString(state)}";

        return Redirect(authorizeUrl);
    }

    [HttpGet("discord/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> DiscordCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(code))
            return BadRequest(new { error = "missing_code" });

        if (string.IsNullOrWhiteSpace(state))
            return BadRequest(new { error = "missing_state" });

        var stateData = _stateStore.ConsumeState(state);
        if (stateData is null)
            return Unauthorized(new { error = "invalid_state" });

        var allowedGuildId = _config["Auth:Discord:AllowedGuildId"];
        if (string.IsNullOrWhiteSpace(allowedGuildId))
            throw new InvalidOperationException("Missing Auth:Discord:AllowedGuildId configuration.");

        var token = await _discord.ExchangeCodeAsync(code, ct);

        if (string.IsNullOrWhiteSpace(token.AccessToken))
            throw new InvalidOperationException(
                $"Discord returned empty access_token. Raw response: {JsonSerializer.Serialize(token)}"
            );

        var isMember = await _discord.IsMemberOfGuildAsync(token.AccessToken, allowedGuildId, ct);
        if (!isMember)
            return StatusCode(403, new { error = "not_in_guild" });

        var me = await _discord.GetCurrentUserAsync(token.AccessToken, ct);

        var avatarUrl = me.Avatar != null
            ? $"https://cdn.discordapp.com/avatars/{me.Id}/{me.Avatar}.png"
            : $"https://cdn.discordapp.com/embed/avatars/{long.Parse(me.Id) % 5}.png";

        var jwt = _jwt.Issue(new JwtUser(
            DiscordUserId: me.Id,
            Username: me.Username,
            Discriminator: me.Discriminator,
            AvatarUrl: avatarUrl
        ));
        
        var frontendUrl = _config["FrontendUrl"] ?? "http://localhost:3000";
        var frontUrl = $"{frontendUrl}/account/callback" +
                       $"?token={Uri.EscapeDataString(jwt.AccessToken)}" +
                       $"&username={Uri.EscapeDataString(me.Username ?? "")}" +
                       $"&avatarUrl={Uri.EscapeDataString(avatarUrl)}";

        return Redirect(frontUrl);
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var discordUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var username = User.FindFirstValue(ClaimTypes.Name);
        var avatarUrl = User.FindFirstValue("avatar_url");

        return Ok(new
        {
            discordUserId,
            username,
            avatarUrl
        });
    }
    
    [HttpGet("validate")]
    [Authorize]
    public IActionResult Validate()
    {
        return Ok(new { valid = true });
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        return Ok(new { ok = true });
    }
}