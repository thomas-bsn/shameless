using System.Text.Json;

namespace Shameless.Companion.Core;

/// <summary>Ce que le module LCU remonte : le client LoL apparaît, disparaît, ou émet un événement de son API locale.</summary>
public abstract record EvenementClientLol
{
    private EvenementClientLol() { }

    public sealed record Detecte : EvenementClientLol;

    /// <summary>Le client s'est fermé (ou a redémarré) : le module le redétectera, avec son nouveau port et mot de passe.</summary>
    public sealed record Perdu : EvenementClientLol;

    /// <summary>Événement WAMP <c>OnJsonApiEvent</c> : uri, type (Create, Update, Delete) et données.</summary>
    public sealed record Message(string Uri, string TypeEvenement, JsonElement Donnees) : EvenementClientLol;
}

public sealed record ReponseLcu(int Statut, string Corps);

/// <summary>
/// Le seam du module LCU (ADR 0004) : la logique ne parle jamais au vrai client, seulement à ceci.
/// Le module (lockfile, authentification, WebSocket) vit dans Shameless.Companion.Lcu.
/// </summary>
public interface IClientLol
{
    /// <summary>
    /// Suit le client LoL tant que le jeton n'est pas annulé : détecte son lancement, écoute ses événements,
    /// le redétecte s'il redémarre. Ne se termine pas d'elle-même.
    /// </summary>
    IAsyncEnumerable<EvenementClientLol> SuivreAsync(CancellationToken ct);

    /// <summary>GET sur l'API locale du client actuellement détecté ; lève <see cref="HttpRequestException"/> s'il n'y en a pas.</summary>
    Task<ReponseLcu> LireAsync(string chemin, CancellationToken ct);
}

public interface IJournal
{
    void Ecrire(string ligne);
}
