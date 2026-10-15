namespace Shameless.Api.Auth;

/// <summary>
/// Une URL de retour est acceptée si c'est un chemin local (« /… » mais pas « //… »)
/// ou une URL absolue http(s) dont l'origine figure dans la liste blanche.
/// </summary>
public static class ReturnUrlPolicy
{
    public static bool IsAllowed(string returnUrl, IReadOnlyList<string> allowedOrigins)
    {
        if (IsLocalPath(returnUrl))
            return true;

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
            return false;

        return allowedOrigins.Any(origin =>
            Uri.TryCreate(origin, UriKind.Absolute, out var allowed)
            && Uri.Compare(uri, allowed, UriComponents.SchemeAndServer, UriFormat.Unescaped, StringComparison.OrdinalIgnoreCase) == 0);
    }

    private static bool IsLocalPath(string url) =>
        url.StartsWith('/') && !url.StartsWith("//") && !url.StartsWith("/\\");
}
