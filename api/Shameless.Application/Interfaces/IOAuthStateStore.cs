namespace Shameless.Api.Interfaces;

public interface IOAuthStateStore
{
    // crée un state unique, stocke returnUrl (TTL court)
    // refresh = rafraîchissement du profil par un utilisateur déjà connecté (la session ne doit jamais en pâtir)
    string CreateState(string? returnUrl, bool refresh = false);

    // consume = single-use (anti-replay). Retourne null si absent/expiré/déjà consommé.
    OAuthStateData? ConsumeState(string state);
}

public sealed record OAuthStateData(string? ReturnUrl, bool Refresh = false);