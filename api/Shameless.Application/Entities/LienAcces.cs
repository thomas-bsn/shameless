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

    /// <summary>
    /// Quand le Capitaine a ouvert son lien pour la première fois (ADR 0017) ; null tant qu'il n'a pas
    /// rejoint, et pour le Lien spectateur. Changer de Capitaine ou renouveler le lien le remet à null.
    /// </summary>
    public DateTime? RejointLe { get; set; }

    /// <summary>
    /// Quand le Capitaine s'est mis « prêt » pour la game à venir ; null tant qu'il ne l'est pas. Remis à
    /// null s'il se remet « pas prêt », quand la game est terminée, et si le Capitaine ou son lien change.
    /// </summary>
    public DateTime? PretLe { get; set; }

    public bool EstPret => PretLe != null;

    public static LienAcces Capitaine(string equipe) => new() { Portee = PorteeLien.Capitaine, Equipe = equipe };

    public static LienAcces Spectateur() => new() { Portee = PorteeLien.Spectateur };

    /// <summary>
    /// Le DM de ce Lien capitaine n'est pas parti (rapporté par le bot, ADR 0017) : la raison, la date
    /// et le joueur qui devait le recevoir. L'organisateur copie alors le lien à la main.
    /// </summary>
    public string? EchecDmRaison { get; set; }
    public DateTime? EchecDmLe { get; set; }
    public int? EchecDmPlayerId { get; set; }

    /// <summary>Remplace le secret : l'ancien lien ne donne plus accès à rien, l'échec de DM est oublié et le Capitaine n'a plus rejoint.</summary>
    public void Renouveler()
    {
        Secret = SecretAleatoire.Nouveau();
        RejointLe = null;
        PretLe = null;
        EchecDmRaison = null;
        EchecDmLe = null;
        EchecDmPlayerId = null;
    }

    public void SignalerEchecDm(string raison, int capitaineId, DateTime maintenant)
    {
        EchecDmRaison = raison;
        EchecDmLe = maintenant;
        EchecDmPlayerId = capitaineId;
    }

    /// <summary>Les raisons d'échec d'un DM que le bot peut rapporter.</summary>
    public static readonly IReadOnlyList<string> RaisonsEchecDm = ["compte_non_lie", "dm_ferme", "autre"];

    /// <summary>Le Capitaine se met prêt (ou ne l'est plus) ; se mettre prêt deux fois garde la première heure.</summary>
    public void DefinirPret(bool pret, DateTime maintenant) => PretLe = pret ? PretLe ?? maintenant : null;

    /// <summary>Mémorise la première ouverture du Lien capitaine ; les suivantes ne changent rien.</summary>
    public void Rejoindre(DateTime maintenant)
    {
        if (Portee == PorteeLien.Capitaine) RejointLe ??= maintenant;
    }
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

/// <summary>
/// Où en est le « prêt » d'un Capitaine : le sien et celui de l'Équipe adverse, et s'il peut encore se
/// déclarer prêt (<c>PretPossible</c> : la Série est en cours et aucune Draft de champions n'est ouverte sur
/// une game en attente, game 1 comme suivantes). Faux partout pour un Lien spectateur.
/// </summary>
public sealed record EtatDuPret(bool Pret, bool AdversairePret, bool PretPossible)
{
    public static readonly EtatDuPret Aucun = new(false, false, false);

    /// <summary>Demande les liens et les games de la Série chargés.</summary>
    public static EtatDuPret De(BestOfSeries series, string equipe) => new(
        series.LienCapitaine(equipe).EstPret,
        series.LienCapitaine(equipe == Equipes.A ? Equipes.B : Equipes.A).EstPret,
        series.IsEnCours && !series.Games.Any(g => g.Status == GameStatuses.Pending && g.DraftChampions != null));
}
