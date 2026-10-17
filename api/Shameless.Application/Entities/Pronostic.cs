namespace Shameless.Application.Entities;

/// <summary>
/// Le pronostic d'un Spectateur sur une Game à venir d'une Série : l'Équipe gagnante, le MVP et le
/// DOFD, chacun facultatif. Aucune Game n'existe avant son lancement (ADR 0013) : le pronostic vise la
/// Série et le numéro de la Game, et se verrouille à son lancement. Le Spectateur est identifié par son
/// ID Discord, sans clé vers <c>users</c>.
/// </summary>
public class Pronostic
{
    public int SeriesId { get; set; }
    public BestOfSeries Series { get; set; } = null!;

    /// <summary>Le numéro de la Game dans la Série, à partir de 1.</summary>
    public int NumeroDansLaSerie { get; set; }

    /// <summary>L'ID Discord (snowflake) du Spectateur.</summary>
    public long PronostiqueurDiscordId { get; set; }

    /// <summary>Le nom affiché du Spectateur sur Discord au moment du pronostic, donné par le bot.</summary>
    public string? PronostiqueurNom { get; set; }

    /// <summary>L'Équipe pronostiquée gagnante, « A » ou « B », null sans choix.</summary>
    public string? EquipeGagnante { get; set; }

    /// <summary>Le Joueur pronostiqué MVP, null sans choix.</summary>
    public int? MvpPlayerId { get; set; }
    public Player? Mvp { get; set; }

    /// <summary>Le Joueur pronostiqué DOFD, null sans choix.</summary>
    public int? DofdPlayerId { get; set; }
    public Player? Dofd { get; set; }

    /// <summary>Le dernier dépôt du pronostic (UTC).</summary>
    public DateTime PronostiqueLe { get; set; }
}
