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
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Cette URL de retour n'est pas autorisée.");

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
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Code OAuth manquant.");

        if (string.IsNullOrWhiteSpace(state))
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Paramètre state manquant.");

        var stateData = _stateStore.ConsumeState(state);
        if (stateData is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Connexion expirée ou invalide, recommence depuis le début.");

        var token = await _discord.ExchangeCodeAsync(code, ct);

        var isMember = await _discord.IsMemberOfGuildAsync(token.AccessToken, _discordOptions.AllowedGuildId, ct);
        if (!isMember)
            return Problem(statusCode: StatusCodes.Status403Forbidden, detail: "Shameless est réservé aux membres du serveur Discord de la communauté.");

        var me = await _discord.GetCurrentUserAsync(token.AccessToken, ct);

        var avatarUrl = me.Avatar != null
            ? $"https://cdn.discordapp.com/avatars/{me.Id}/{me.Avatar}.png"
            : $"https://cdn.discordapp.com/embed/avatars/{long.Parse(me.Id) % 5}.png";

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, me.Id),
            new Claim(ClaimTypes.Name, me.Username),
            new Claim(ClaimsPrincipalExtensions.AvatarClaim, avatarUrl)
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
    public async Task<IActionResult> GetCurrentUser([FromServices] IAuthorizationService authorization)
    {
        return Ok(new AuthUserResponse(
            Valid: true,
            DiscordUserId: User.DiscordId(),
            Username: User.Username(),
            AvatarUrl: User.AvatarUrl(),
            IsAdmin: await authorization.IsAdminAsync(User)
        ));
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
