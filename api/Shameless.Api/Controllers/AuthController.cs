using System.Security.Claims;
using Shameless.Api.Auth;
using Shameless.Api.Interfaces;
using Shameless.Api.Models.Responses.Auth;
using Shameless.Infrastructure.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Shameless.Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private const string AvatarClaim = "avatar_url";

    private readonly DiscordOAuthOptions _discordOptions;
    private readonly AuthOptions _authOptions;
    private readonly IOAuthStateStore _stateStore;
    private readonly IDiscordOAuthClient _discord;

    public AuthController(
        IOptions<DiscordOAuthOptions> discordOptions,
        IOptions<AuthOptions> authOptions,
        IOAuthStateStore stateStore,
        IDiscordOAuthClient discord)
    {
        _discordOptions = discordOptions.Value;
        _authOptions = authOptions.Value;
        _stateStore = stateStore;
        _discord = discord;
    }

    [HttpGet("discord/login")]
    [AllowAnonymous]
    public IActionResult DiscordLogin([FromQuery] string? returnUrl = null)
    {
        if (returnUrl is not null && !ReturnUrlPolicy.IsAllowed(returnUrl, _authOptions.ReturnOrigins))
            return BadRequest(new { error = "invalid_return_url" });

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

        var token = await _discord.ExchangeCodeAsync(code, ct);

        var isMember = await _discord.IsMemberOfGuildAsync(token.AccessToken, _discordOptions.AllowedGuildId, ct);
        if (!isMember)
            return StatusCode(403, new { error = "not_in_guild" });

        var me = await _discord.GetCurrentUserAsync(token.AccessToken, ct);

        var avatarUrl = me.Avatar != null
            ? $"https://cdn.discordapp.com/avatars/{me.Id}/{me.Avatar}.png"
            : $"https://cdn.discordapp.com/embed/avatars/{long.Parse(me.Id) % 5}.png";

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, me.Id),
            new Claim(ClaimTypes.Name, me.Username),
            new Claim(AvatarClaim, avatarUrl)
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        // Validée au login, revérifiée ici au cas où la liste blanche aurait changé entre-temps.
        var returnUrl = stateData.ReturnUrl is { } url && ReturnUrlPolicy.IsAllowed(url, _authOptions.ReturnOrigins)
            ? url
            : "/";

        return Redirect(returnUrl);
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetCurrentUser()
    {
        return Ok(new AuthUserResponse(
            Valid: true,
            DiscordUserId: User.FindFirstValue(ClaimTypes.NameIdentifier),
            Username: User.FindFirstValue(ClaimTypes.Name),
            AvatarUrl: User.FindFirstValue(AvatarClaim)
        ));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
