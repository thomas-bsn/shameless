namespace Shameless.Application.Entities;

public class User
{
    public int Id { get; set; }
    public long DiscordId { get; set; }
    public string? DiscordUsername { get; set; }
    public string? AvatarUrl { get; set; }
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
