using Shameless.Application.Picker;

namespace Shameless.Application.Stats;

/// <summary>D'où vient le rôle retenu pour noter un Joueur sur une Game.</summary>
public enum SourceDuRole
{
    /// <summary>La position donnée par les stats de la partie (Companion).</summary>
    Partie,

    /// <summary>Le rôle attribué par l'Autorole dans la Série.</summary>
    Autorole,

    /// <summary>Le rôle principal du Joueur en SoloQ.</summary>
    RolePrincipal,

    /// <summary>Aucun rôle connu : la note utilise le profil polyvalent.</summary>
    Inconnue
}

/// <summary>Le rôle selon lequel un Joueur est noté pour une Game, et sa source.</summary>
public sealed record RoleJoue(Role? Role, SourceDuRole Source)
{
    /// <summary>
    /// Le premier rôle connu, dans l'ordre : position de la partie, rôle de l'Autorole, rôle principal SoloQ.
    /// </summary>
    public static RoleJoue Determiner(Role? positionDeLaPartie, Role? autorole = null, Role? rolePrincipal = null) =>
        positionDeLaPartie is { } partie ? new(partie, SourceDuRole.Partie)
        : autorole is { } auto ? new(auto, SourceDuRole.Autorole)
        : rolePrincipal is { } principal ? new(principal, SourceDuRole.RolePrincipal)
        : new(null, SourceDuRole.Inconnue);
}
