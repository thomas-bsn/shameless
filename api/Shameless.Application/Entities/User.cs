namespace Shameless.Application.Entities;

public class User
{
    public int Id { get; set; }
    public long DiscordId { get; set; }
    public string? DiscordUsername { get; set; }
    public string? AvatarUrl { get; set; }

    /// <summary>Bannière Discord (GIF si animée) ; null si l'Utilisateur n'en a pas.</summary>
    public string? BannerUrl { get; set; }

    /// <summary>Couleur d'accent Discord (« #RRGGBB »), repli quand il n'y a pas de bannière.</summary>
    public string? BannerColor { get; set; }
    public Player? Player { get; set; }

    public UserRole Role { get; set; } = UserRole.Utilisateur;

    /// <summary>Qui a donné le rôle actuel : un pseudo, ou « ligne de commande » / « configuration ». « Promu par X, le JJ/MM/AAAA » (ADR 0017).</summary>
    public string? PromotedBy { get; set; }
    public DateTime? PromotedAt { get; set; }
}

/// <summary>Les trois rôles de l'ADR 0017. L'ordre compte : un rôle supérieur englobe les droits du précédent.</summary>
public enum UserRole
{
    Utilisateur,
    Administrateur,
    Superadministrateur
}
