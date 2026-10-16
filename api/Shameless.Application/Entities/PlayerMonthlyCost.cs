using Shameless.Application.Cost;

namespace Shameless.Application.Entities;

/// <summary>
/// Le cost d'un Joueur pour un mois (ADR 0012) : mis à jour après chaque Game terminée du mois, il
/// devient le cost final du mois une fois celui-ci passé, et le cost de départ du mois suivant.
/// </summary>
public class PlayerMonthlyCost
{
    public int Id { get; set; }
    public int PlayerId { get; set; }

    /// <summary>Le premier jour du mois (2025-01-01, 2025-02-01…).</summary>
    public DateTime Month { get; set; }

    /// <summary>Le cost du mois ; une fois le mois passé, son cost final.</summary>
    public decimal FinalCost { get; set; }

    /// <summary>Le cost de départ du mois ; null pour les mois repris de l'ancienne application.</summary>
    public decimal? CostDeDepart { get; set; }

    /// <summary>
    /// D'où vient le cost de départ ; null pour les mois repris de l'ancienne application. Un cost
    /// fixé par un admin reste le départ du mois quand les mois précédents sont recalculés.
    /// </summary>
    public SourceDuCostDeDepart? SourceDuCostDeDepart { get; set; }

    /// <summary>La moyenne des notes de performance du mois ; null sans Game notée.</summary>
    public decimal? NoteMoyenne { get; set; }

    /// <summary>Ancien modèle MMR (ancienne application) : 0 pour les mois calculés depuis la Phase 1c.</summary>
    public int BaseMMR { get; set; }

    /// <summary>Ancien modèle MMR (ancienne application) : 0 pour les mois calculés depuis la Phase 1c.</summary>
    public int WinrateAdjustment { get; set; }

    /// <summary>Les Games terminées du mois.</summary>
    public int GamesPlayed { get; set; }
    public int Wins { get; set; }

    // Navigation property
    public Player Player { get; set; } = null!;
}