namespace Shameless.Application.Entities;

public class CustomGameParticipant
{
    public int Id { get; set; }
    public int CustomGameId { get; set; }
    public int PlayerId { get; set; }

    /// <summary>Le Side joué par le participant dans cette game : "blue" | "red".</summary>
    public string Side { get; set; } = string.Empty;

    /// <summary>Le champion réellement joué (id Data Dragon, ex. « LeeSin »), donné par le rapport du Companion.</summary>
    public string? ChampionId { get; set; }

    /// <summary>Les stats brutes du joueur pour la partie (JSON), données par le rapport du Companion.</summary>
    public string? RiotParticipantData { get; set; }

    /// <summary>Les corrections d'un Administrateur (une par champ), stockées à part des données du Companion.</summary>
    public ICollection<CorrectionDeStat> Corrections { get; set; } = new List<CorrectionDeStat>();

    // Navigation properties
    public CustomGame CustomGame { get; set; } = null!;
    public Player Player { get; set; } = null!;

    /// <summary>L'Équipe du participant a gagné cette game.</summary>
    public bool HasWon => CustomGame.WinningTeam != null && CustomGame.EquipeOn(Side) == CustomGame.WinningTeam;
}
