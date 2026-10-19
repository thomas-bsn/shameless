using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Shameless.Companion.Core;

/// <summary>L'URL de l'Instance et le Code Companion saisis par le joueur.</summary>
public sealed record IdentifiantsCompanion(string UrlInstance, string Code);

/// <summary>Où l'app garde ses identifiants : DPAPI sous Windows, mémoire dans les tests.</summary>
public interface IStockageIdentifiants
{
    IdentifiantsCompanion? Lire();
    void Ecrire(IdentifiantsCompanion identifiants);
    void Effacer();
}

/// <summary>La Série que l'API a reconnue pour le Code (GET /companion/{code}).</summary>
public sealed record SerieDuCompanion(
    int SeriesId,
    int Format,
    bool Fearless,
    int TeamAWins,
    int TeamBWins,
    IReadOnlyList<string> EquipeA,
    IReadOnlyList<string> EquipeB);

public abstract record Connexion
{
    private Connexion() { }

    /// <summary>Aucun Code saisi (ou Code effacé).</summary>
    public sealed record NonConnecte : Connexion;

    public sealed record SerieReconnue(SerieDuCompanion Serie) : Connexion;

    /// <summary>L'API ne connaît pas ce Code (inconnu, renouvelé, Série terminée) : il a été effacé.</summary>
    public sealed record CodeRefuse : Connexion;

    /// <summary>L'API n'a pas répondu correctement (réseau, erreur serveur) : le Code est gardé.</summary>
    public sealed record Injoignable(string Message) : Connexion;
}

/// <param name="ClientLolDetecte">Valeur fixe tant que la détection du client LoL n'existe pas.</param>
public sealed record EtatCompanion(Connexion Connexion, bool ClientLolDetecte = true);

public sealed class ConnexionCompanion(IStockageIdentifiants stockage, HttpClient http)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    /// <summary>Au lancement : relit les identifiants stockés et reconnaît la Série.</summary>
    public async Task<EtatCompanion> DemarrerAsync(CancellationToken ct = default)
    {
        var identifiants = stockage.Lire();
        return identifiants is null
            ? new EtatCompanion(new Connexion.NonConnecte())
            : await SeConnecterAsync(identifiants.UrlInstance, identifiants.Code, ct);
    }

    public async Task<EtatCompanion> SeConnecterAsync(string urlInstance, string code, CancellationToken ct = default)
    {
        var url = $"{urlInstance.TrimEnd('/')}/companion/{Uri.EscapeDataString(code)}";
        try
        {
            using var reponse = await http.GetAsync(url, ct);
            if (reponse.StatusCode == HttpStatusCode.NotFound)
            {
                stockage.Effacer();
                return new EtatCompanion(new Connexion.CodeRefuse());
            }
            reponse.EnsureSuccessStatusCode();
            var serie = await reponse.Content.ReadFromJsonAsync<SerieDuCompanion>(JsonOptions, ct);
            stockage.Ecrire(new IdentifiantsCompanion(urlInstance, code));
            return new EtatCompanion(new Connexion.SerieReconnue(serie!));
        }
        catch (Exception e) when (e is HttpRequestException or JsonException or InvalidOperationException or UriFormatException
            || (e is TaskCanceledException && !ct.IsCancellationRequested))
        {
            return new EtatCompanion(new Connexion.Injoignable(e.Message));
        }
    }
}
