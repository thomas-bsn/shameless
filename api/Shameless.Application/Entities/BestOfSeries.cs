namespace Shameless.Application.Entities;

/// <summary>
/// Une Série : une suite de games entre l'Équipe A et l'Équipe B, au format BO1, BO2, BO3 ou BO5.
/// Ses joueurs (<see cref="Players"/>) sont la composition courante des deux Équipes, celle de la
/// prochaine game ; la composition de chaque game jouée reste dans ses participants.
/// </summary>
public class BestOfSeries
{
    public static readonly IReadOnlyList<int> Formats = [1, 2, 3, 5];

    public int Id { get; set; }
    public DateTime PlayedAt { get; set; }
    public int Format { get; set; } // 1, 2, 3, 5

    public int TeamAWins { get; set; } = 0;
    public int TeamBWins { get; set; } = 0;

    /// <summary>Simple étiquette : la Série se joue en Fearless.</summary>
    public bool Fearless { get; set; }

    /// <summary>Les Sides s'inversent automatiquement à chaque game.</summary>
    public bool AlternateSides { get; set; }

    public SeriesOutcome Outcome { get; set; } = SeriesOutcome.EnCours;

    /// <summary>L'Équipe gagnante ("A" | "B") quand la Série est gagnée, sinon null.</summary>
    public string? WinningTeam { get; set; }

    public int? SoireeId { get; set; }
    public Soiree? Soiree { get; set; }

    public ICollection<CustomGame> Games { get; set; } = new List<CustomGame>();
    public ICollection<SeriesPlayer> Players { get; set; } = new List<SeriesPlayer>();

    public bool IsEnCours => Outcome == SeriesOutcome.EnCours;

    /// <summary>Victoires nécessaires pour gagner : 1, 2, 2, 3 pour BO1, BO2, BO3, BO5.</summary>
    public int WinsNeeded => Format / 2 + 1;

    public static bool IsValidFormat(int format) => Formats.Contains(format);

    /// <summary>
    /// Recompte le score à partir des games terminées et en déduit l'issue : gagnée quand une
    /// Équipe atteint le nombre de victoires du format, nulle pour un BO2 à 1-1.
    /// </summary>
    public void ApplyResults(IEnumerable<CustomGame> games)
    {
        var completed = games.Where(g => g.Status == "completed").ToList();
        TeamAWins = completed.Count(g => g.WinningTeam == Equipes.A);
        TeamBWins = completed.Count(g => g.WinningTeam == Equipes.B);

        if (TeamAWins >= WinsNeeded || TeamBWins >= WinsNeeded)
        {
            Outcome = SeriesOutcome.Gagnee;
            WinningTeam = TeamAWins >= WinsNeeded ? Equipes.A : Equipes.B;
        }
        else if (Format == 2 && TeamAWins == 1 && TeamBWins == 1)
        {
            Outcome = SeriesOutcome.Nulle;
        }
    }

    public IEnumerable<SeriesPlayer> PlayersOf(string equipe) => Players.Where(p => p.Equipe == equipe);
}

/// <summary>L'issue d'une Série.</summary>
public enum SeriesOutcome
{
    EnCours,
    Gagnee,
    Nulle,
    Interrompue
}
