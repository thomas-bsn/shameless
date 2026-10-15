namespace Custom5v5.Api.Interfaces;

public interface IOAuthStateStore
{
    // crée un state unique, stocke returnUrl (TTL court)
    string CreateState(string? returnUrl);

    // consume = single-use (anti-replay). Retourne null si absent/expiré/déjà consommé.
    OAuthStateData? ConsumeState(string state);
}

public sealed record OAuthStateData(string? ReturnUrl);