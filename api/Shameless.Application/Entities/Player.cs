namespace Shameless.Application.Entities;

public class Player
{
    public int Id { get; set; }
    public string PUUID { get; set; } = null!;  
    public string Prenom { get; set; } = null!;
    public string RiotId { get; set; } = null!;
    public string? RankTier { get; set; }
    public int? RankDivision { get; set; }
    public string? MainRole { get; set; }
    public DateTime CreatedAt { get; set; }
    public int? LP { get; set; }

    /// <summary>Rang actuel en file Flex (RANKED_FLEX_SR), même format que le Rang SoloQ ; null si non classé.</summary>
    public string? FlexRankTier { get; set; }
    public int? FlexRankDivision { get; set; }
    public int? FlexLp { get; set; }
    /// <summary>
    /// Le Discord principal (ADR 0018) : l'Utilisateur lié depuis le plus longtemps, dont la photo et l'identité
    /// s'affichent partout. Dérivé de <see cref="Users"/> par le dépôt à chaque lien ou déliaison ; null sans lien.
    /// </summary>
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Tous les Utilisateurs liés à ce Joueur, sans vérification (ADR 0018). Chargé à la demande.</summary>
    public ICollection<User> Users { get; set; } = new List<User>();
    public string? PeakTier { get; set; }
    public int? PeakDivision { get; set; }
    public string? PeakSeason { get; set; }
    public int PeakLp { get; set; } = 0;
    
    public decimal CurrentCost { get; set; }
    public bool IsPlacement { get; set; }
    
    public ICollection<CustomGameParticipant> CustomGameParticipations { get; set; } = new List<CustomGameParticipant>();
    public ICollection<PlayerMonthlyCost> MonthlyCosts { get; set; } = new List<PlayerMonthlyCost>();
}