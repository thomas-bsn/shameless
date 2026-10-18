using System.Security.Claims;
using Shameless.Api.Auth;
using Shameless.Api.Interfaces;
using Shameless.Api.Models.Responses.Auth;
using Shameless.Application.Entities;
using Shameless.Application.Interfaces;
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
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult DiscordLogin([FromQuery] string? returnUrl = null)
    {
        if (returnUrl is not null && !ReturnUrlPolicy.IsAllowed(returnUrl, _authOptions.ReturnOrigins))
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Cette URL de retour n'est pas autorisée.");

        return Redirect(AuthorizeUrl(_stateStore.CreateState(returnUrl), silent: false));
    }

    /// <summary>
    /// Relit pseudo, photo et bannière sur Discord sans reconnecter l'utilisateur : renvoie vers Discord avec
    /// <c>prompt=none</c> (aucun écran si l'application est déjà autorisée), qui revient sur le callback.
    /// Aucun jeton Discord n'est gardé dans le cookie : il expirerait avant la session.
    /// </summary>
    [HttpGet("discord/refresh")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    public IActionResult DiscordRefresh([FromQuery] string? returnUrl = null)
    {
        if (returnUrl is not null && !ReturnUrlPolicy.IsAllowed(returnUrl, _authOptions.ReturnOrigins))
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Cette URL de retour n'est pas autorisée.");

        return Redirect(AuthorizeUrl(_stateStore.CreateState(returnUrl, refresh: true), silent: true));
    }

    private string AuthorizeUrl(string state, bool silent) =>
        "https://discord.com/api/oauth2/authorize" +
        $"?client_id={Uri.EscapeDataString(_discordOptions.ClientId)}" +
        $"&redirect_uri={Uri.EscapeDataString(_discordOptions.RedirectUri)}" +
        "&response_type=code" +
        $"&scope={Uri.EscapeDataString("identify guilds")}" +
        $"&state={Uri.EscapeDataString(state)}" +
        (silent ? "&prompt=none" : "");

    [HttpGet("discord/callback")]
    [AllowAnonymous]
    [ApiExplorerSettings(IgnoreApi = true)]
    public async Task<IActionResult> DiscordCallback(
        [FromQuery] string? code,
        [FromQuery] string? state,
        [FromQuery] string? error,
        [FromServices] IUserRepository users,
        [FromServices] ILogger<AuthController> logger,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(state))
            return Problem(statusCode: StatusCodes.Status400BadRequest,
                detail: string.IsNullOrWhiteSpace(code) ? "Code OAuth manquant." : "Paramètre state manquant.");

        var stateData = _stateStore.ConsumeState(state);

        // Rafraîchissement refusé par Discord (autorisation silencieuse impossible) : la session reste telle quelle.
        if (stateData is { Refresh: true } && (!string.IsNullOrWhiteSpace(error) || string.IsNullOrWhiteSpace(code)))
            return Redirect(SafeReturnUrl(stateData));

        if (string.IsNullOrWhiteSpace(code))
            return Problem(statusCode: StatusCodes.Status400BadRequest, detail: "Code OAuth manquant.");

        if (stateData is null)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, detail: "Connexion expirée ou invalide, recommence depuis le début.");

        DiscordMeResponse me;
        try
        {
            var token = await _discord.ExchangeCodeAsync(code, ct);

            var isMember = await _discord.IsMemberOfGuildAsync(token.AccessToken, _discordOptions.AllowedGuildId, ct);
            if (!isMember)
            {
                if (stateData.Refresh) return Redirect(SafeReturnUrl(stateData));
                return Problem(statusCode: StatusCodes.Status403Forbidden, detail: "Shameless est réservé aux membres du serveur Discord de la communauté.");
            }

            me = await _discord.GetCurrentUserAsync(token.AccessToken, ct);
        }
        catch (Exception ex) when (stateData.Refresh && ex is not OperationCanceledException)
        {
            // Un échec Discord ne casse pas la session : on revient sur la page, profil inchangé.
            logger.LogWarning(ex, "Rafraîchissement du profil Discord impossible.");
            return Redirect(SafeReturnUrl(stateData));
        }

        // Un rafraîchissement ne change jamais de compte : il relit seulement le profil de la session en cours.
        if (stateData.Refresh && User.DiscordIdOrNull() is { } current && current.ToString() != me.Id)
            return Redirect(SafeReturnUrl(stateData));

        var avatarUrl = me.Avatar != null
            ? $"https://cdn.discordapp.com/avatars/{me.Id}/{me.Avatar}.png"
            : $"https://cdn.discordapp.com/embed/avatars/{long.Parse(me.Id) % 5}.png";
        var bannerUrl = me.Banner is null
            ? null
            : $"https://cdn.discordapp.com/banners/{me.Id}/{me.Banner}.{(me.Banner.StartsWith("a_") ? "gif" : "png")}";
        var bannerColor = me.AccentColor is { } accent ? $"#{accent & 0xFFFFFF:X6}" : null;

        var identity = new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, me.Id),
            new Claim(ClaimTypes.Name, me.Username),
            new Claim(ClaimsPrincipalExtensions.AvatarClaim, avatarUrl)
        ], CookieAuthenticationDefaults.AuthenticationScheme);

        // La ligne Utilisateur naît à la première connexion ; pseudo, photo et bannière sont relus à chaque connexion.
        await users.SignInAsync(long.Parse(me.Id), me.Username, avatarUrl, bannerUrl, bannerColor);

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = true });

        return Redirect(SafeReturnUrl(stateData));
    }

    // Validée au login, revérifiée ici au cas où la liste blanche aurait changé entre-temps.
    private string SafeReturnUrl(OAuthStateData stateData) =>
        stateData.ReturnUrl is { } url && ReturnUrlPolicy.IsAllowed(url, _authOptions.ReturnOrigins)
            ? url
            : "/";

    [HttpGet("me")]
    [ProducesResponseType<AuthUserResponse>(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCurrentUser([FromServices] IUserRepository users)
    {
        // Le profil vient de la base (relu à chaque connexion et au rafraîchissement), pas des claims figés du cookie.
        var row = await users.GetByDiscordIdAsync(User.DiscordIdOrNull() ?? 0);
        var role = row?.Role ?? UserRole.Utilisateur;
        return Ok(new AuthUserResponse(
            Valid: true,
            DiscordUserId: User.DiscordId(),
            Username: row?.DiscordUsername ?? User.Username(),
            AvatarUrl: row?.AvatarUrl ?? User.AvatarUrl(),
            IsAdmin: role is UserRole.Administrateur or UserRole.Superadministrateur,
            Role: role,
            BannerUrl: row?.BannerUrl,
            BannerColor: row?.BannerColor
        ));
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return NoContent();
    }
}
