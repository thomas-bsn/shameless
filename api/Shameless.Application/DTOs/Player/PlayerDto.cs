namespace Shameless.Application.DTOs.Players;

public class PlayerDto
{
    public int Id { get; set; }
    public required string Prenom { get; set; }
    public required string RiotId { get; set; }
    public required string? Puuid { get; set; }
    public string? RankTier { get; set; }
    public int? RankDivision { get; set; }
    public string? MainRole { get; set; }
    public int? Lp { get; set; }
    /// <summary>Rang actuel en file Flex ; null si le Joueur n'y est pas classé.</summary>
    public string? FlexRankTier { get; set; }
    public int? FlexRankDivision { get; set; }
    public int? FlexLp { get; set; }
    public string? PeakTier { get; set; }
    public int? PeakDivision { get; set; }
    public string? PeakSeason { get; set; }
    public int PeakLp { get; set; } = 0;
    public decimal CurrentCost { get; set; }
    /// <summary>
    /// Évolution du cost depuis le cost final du dernier mois enregistré avant le mois courant
    /// (positive : le Joueur monte) ; null sans mois précédent. Renseignée par la liste des Joueurs.
    /// </summary>
    public decimal? CostEvolution { get; set; }
}