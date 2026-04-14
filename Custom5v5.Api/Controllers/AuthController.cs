using System.Security.Claims;
using Custom5v5.Api.Contracts.Matches;
using Custom5v5.Api.Interfaces;
using Custom5v5.Api.Models.Responses.Auth;
using Custom5v5.Application.Interfaces;
using Custom5v5.Infrastructure.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Custom5v5.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly DiscordOAuthOptions _discordOptions;
    private readonly IOAuthStateStore _stateStore;
    private readonly IDiscordOAuthClient _discord;
    private readonly IJwtIssuer _jwt;

    public AuthController(
        IOptions<DiscordOAuthOptions> discordOptions,
        IOAuthStateStore stateStore,
        IDiscordOAuthClient discord,
        IJwtIssuer jwt)
    {
        _discordOptions = discordOptions.Value;
        _stateStore = stateStore;
        _discord = discord;
        _jwt = jwt;
    }

    [HttpGet("discord/login")]
    [AllowAnonymous]
    public IActionResult DiscordLogin([FromQuery] string? returnUrl = null)
    {
        var state = _stateStore.CreateState(returnUrl);
        var scope = "identify guilds";

        var authorizeUrl =
            "https://discord.com/api/oauth2/authorize" +
            $"?client_id={Uri.EscapeDataString(_discordOptions.ClientId)}" +
            $"&redirect_uri={Uri.EscapeDataString(_discordOptions.RedirectUri)}" +
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

        if (string.IsNullOrWhiteSpace(_discordOptions.AllowedGuildId))
            throw new InvalidOperationException("Missing Auth:Discord:AllowedGuildId configuration.");

        var token = await _discord.ExchangeCodeAsync(code, ct);
        
        var isMember = await _discord.IsMemberOfGuildAsync(token.AccessToken, _discordOptions.AllowedGuildId, ct);
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

        var redirectUrl = $"{_discordOptions.FrontendUrl}/account/callback" +
                          $"?token={Uri.EscapeDataString(jwt.AccessToken)}" +
                          $"&username={Uri.EscapeDataString(me.Username ?? "")}" +
                          $"&avatarUrl={Uri.EscapeDataString(avatarUrl)}";

        return Redirect(redirectUrl);
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
        var discordUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        var username = User.FindFirstValue(ClaimTypes.Name);
        var avatarUrl = User.FindFirstValue("avatar_url");

        return Ok(new AuthUserResponse(
            Valid: true,
            DiscordUserId: discordUserId,
            Username: username,
            AvatarUrl: avatarUrl
        ));
    }

    [HttpPost("logout")]
    [Authorize]
    public IActionResult Logout()
    {
        return Ok(new { ok = true });
    }
}