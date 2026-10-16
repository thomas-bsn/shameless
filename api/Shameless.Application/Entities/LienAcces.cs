using System.Buffers.Text;
using System.Security.Cryptography;

namespace Shameless.Application.Entities;

/// <summary>
/// Un lien d'accès à une Série (ADR 0010) : un Lien capitaine par Équipe, valable pour toute la Série,
/// et un Lien spectateur en lecture seule. Le lien est un secret aléatoire long, porté par l'URL ;
/// l'API le garde pour le réafficher à l'organisateur, ne l'expose à personne d'autre et n'en tire ni
/// session ni cookie.
/// </summary>
public class LienAcces
{
    public int Id { get; set; }
    public int SeriesId { get; set; }
    public BestOfSeries Series { get; set; } = null!;

    public PorteeLien Portee { get; set; }

    /// <summary>L'Équipe d'un Lien capitaine ("A" | "B") ; null pour le Lien spectateur.</summary>
    public string? Equipe { get; set; }

    public string Secret { get; set; } = SecretAleatoire.Nouveau();

    public static LienAcces Capitaine(string equipe) => new() { Portee = PorteeLien.Capitaine, Equipe = equipe };

    public static LienAcces Spectateur() => new() { Portee = PorteeLien.Spectateur };

    /// <summary>Remplace le secret : l'ancien lien ne donne plus accès à rien.</summary>
    public void Renouveler() => Secret = SecretAleatoire.Nouveau();
}

/// <summary>Le secret d'un lien d'accès ou d'un code Companion.</summary>
public static class SecretAleatoire
{
    /// <summary>256 bits d'aléa cryptographique, en base64url (43 caractères).</summary>
    public static string Nouveau() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));
}

/// <summary>Ce qu'un lien permet dans sa Série.</summary>
public enum PorteeLien
{
    /// <summary>Jouer la Draft de champions de son Équipe et la lire.</summary>
    Capitaine,

    /// <summary>Lire les Drafts de champions, sans aucune action.</summary>
    Spectateur
}

/// <summary>
/// L'accès que donne un lien valable : sa Série, sa portée et, pour un Lien capitaine, son Équipe.
/// C'est la seule chose qu'une commande ou une lecture par lien connaît de l'appelant.
/// </summary>
public sealed record AccesParLien(int SeriesId, PorteeLien Portee, string? Equipe);
