namespace Shameless.Application.Entities;

/// <summary>
/// Un message du chat de la Draft de champions. Il est écrit par un Capitaine (sous le prénom de son
/// Joueur), un spectateur connecté (sous son nom Discord) ou un spectateur sans connexion (« Spectateur N »).
/// Il disparaît avec sa Draft de champions et n'est plus affiché une fois celle-ci terminée.
/// </summary>
public class MessageDraftChampions
{
    public const int LongueurMax = 300;

    /// <summary>Débit maximal par émetteur : <see cref="MessagesParFenetre"/> messages sur <see cref="Fenetre"/>.</summary>
    public const int MessagesParFenetre = 5;
    public static readonly TimeSpan Fenetre = TimeSpan.FromSeconds(10);

    public const string PrefixeCapitaine = "capitaine:";
    public const string PrefixeDiscord = "discord:";
    public const string PrefixeAnonyme = "anonyme:";

    public int Id { get; set; }
    public int DraftChampionsId { get; set; }

    /// <summary>Qui écrit, pour limiter le débit et garder un pseudo stable : capitaine:A, discord:ID ou anonyme:jeton.</summary>
    public string Emetteur { get; set; } = "";

    public string Auteur { get; set; } = "";
    public string? AvatarUrl { get; set; }

    /// <summary>L'Équipe du Capitaine qui écrit ; null pour un spectateur.</summary>
    public string? Equipe { get; set; }

    public string Contenu { get; set; } = "";
    public DateTime EnvoyeLe { get; set; }
}
