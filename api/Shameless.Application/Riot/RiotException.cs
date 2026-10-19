namespace Shameless.Application.Riot;

/// <summary>Pourquoi Riot n'a pas donné de réponse exploitable.</summary>
public enum CauseEchecRiot
{
    /// <summary>401 ou 403 : la clé Riot est refusée (invalide ou expirée).</summary>
    CleRefusee,
    /// <summary>429 : limite de requêtes atteinte.</summary>
    LimiteAtteinte,
    /// <summary>5xx : panne côté Riot.</summary>
    Panne,
    /// <summary>404 : Riot ne connaît pas ce compte.</summary>
    Introuvable,
    /// <summary>Délai dépassé, réseau coupé ou disjoncteur ouvert après des échecs répétés.</summary>
    Injoignable,
    /// <summary>
    /// 400 « Exception decrypting » : un PUUID est chiffré par clé d'API, et celui du joueur vient d'une autre clé.
    /// À résoudre à nouveau depuis son RiotId.
    /// </summary>
    PuuidPerime,
    /// <summary>Toute autre réponse inattendue (400, 405, ...).</summary>
    Inattendue
}

/// <summary>
/// Riot a échoué, avec la cause. L'appelant garde alors les anciennes valeurs : c'est différent d'une
/// réponse sans donnée (pas de match, pas de rang), qui n'est pas une erreur.
/// </summary>
public sealed class RiotException(CauseEchecRiot cause, int? statut = null, TimeSpan? reessayerApres = null, Exception? inner = null, string? detail = null)
    : Exception(detail is null ? Decrire(cause, statut, reessayerApres) : $"{Decrire(cause, statut, reessayerApres)} ; {detail}", inner)
{
    public CauseEchecRiot Cause { get; } = cause;
    public int? Statut { get; } = statut;
    /// <summary>Le délai demandé par Riot (en-tête Retry-After) pour une limite atteinte.</summary>
    public TimeSpan? ReessayerApres { get; } = reessayerApres;

    /// <summary>Au-delà de ce Retry-After, attendre bloquerait l'écran : la 429 n'est pas reprise.</summary>
    public static readonly TimeSpan AttenteMaximale = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Insister est inutile : la clé est refusée (pour tout le monde), ou Riot demande d'attendre plus longtemps
    /// que <see cref="AttenteMaximale"/>. Une actualisation de plusieurs joueurs s'arrête alors.
    /// </summary>
    public bool Bloque => Cause == CauseEchecRiot.CleRefusee
        || (Cause == CauseEchecRiot.LimiteAtteinte && ReessayerApres > AttenteMaximale);

    private static string Decrire(CauseEchecRiot cause, int? statut, TimeSpan? reessayerApres) => cause switch
    {
        CauseEchecRiot.CleRefusee => $"clé Riot refusée ({statut ?? 403}), elle est invalide ou expirée",
        CauseEchecRiot.LimiteAtteinte => reessayerApres is { } d
            ? $"limite de requêtes Riot atteinte (429), réessayer dans {Math.Ceiling(d.TotalSeconds)} s"
            : "limite de requêtes Riot atteinte (429)",
        CauseEchecRiot.Panne => $"panne côté Riot ({statut})",
        CauseEchecRiot.Introuvable => "joueur introuvable chez Riot (404)",
        CauseEchecRiot.PuuidPerime => "PUUID périmé (clé Riot différente de celle d'origine), à resynchroniser",
        CauseEchecRiot.Injoignable => "Riot injoignable (délai dépassé ou service coupé)",
        _ => statut is null ? "réponse inattendue de Riot" : $"réponse inattendue de Riot ({statut})"
    };
}
