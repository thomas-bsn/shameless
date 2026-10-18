using Shameless.Application.Picker;

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

    public SeriesOutcome Outcome { get; set; } = SeriesOutcome.EnCours;

    /// <summary>L'Équipe gagnante ("A" | "B") quand la Série est gagnée, sinon null.</summary>
    public string? WinningTeam { get; set; }

    public int? SoireeId { get; set; }
    public Soiree? Soiree { get; set; }

    /// <summary>
    /// Jeton de concurrence : chaque commande sur la Série l'incrémente (<see cref="Touch"/>) ; son
    /// enregistrement est refusé si une commande simultanée l'a incrémenté entre-temps.
    /// </summary>
    public int Version { get; set; }

    public ICollection<CustomGame> Games { get; set; } = new List<CustomGame>();
    public ICollection<SeriesPlayer> Players { get; set; } = new List<SeriesPlayer>();

    /// <summary>Les liens d'accès (ADR 0010) : un Lien capitaine par Équipe et le Lien spectateur.</summary>
    public ICollection<LienAcces> Liens { get; set; } = new List<LienAcces>();

    /// <summary>
    /// Le code Companion : un secret long, distinct des liens d'accès, qui ne permet que de téléverser
    /// des rapports pour cette Série, et seulement tant qu'elle est en cours.
    /// </summary>
    public string CodeCompanion { get; set; } = SecretAleatoire.Nouveau();

    /// <summary>Remplace le code Companion : l'ancien est refusé.</summary>
    public void RenouvelerCodeCompanion() => CodeCompanion = SecretAleatoire.Nouveau();

    public LienAcces LienCapitaine(string equipe) =>
        Liens.Single(l => l.Portee == PorteeLien.Capitaine && l.Equipe == equipe);

    /// <summary>
    /// L'échec de DM du Lien capitaine de l'Équipe, tant qu'il concerne son Capitaine actuel : changer de
    /// Capitaine, comme renouveler le lien, l'oublie. Demande les liens chargés.
    /// </summary>
    public LienAcces? LienEnEchecDm(string equipe) =>
        LienCapitaine(equipe) is { EchecDmRaison: not null } lien && lien.EchecDmPlayerId == CapitaineOf(equipe) ? lien : null;

    public LienAcces LienSpectateur => Liens.Single(l => l.Portee == PorteeLien.Spectateur);

    /// <summary>L'accès que donne ce lien de la Série (ADR 0017 : le Lien spectateur vaut dès la création).</summary>
    public AccesParLien AccesPar(LienAcces lien) => new(Id, lien.Portee, lien.Equipe);

    public bool IsEnCours => Outcome == SeriesOutcome.EnCours;

    /// <summary>Victoires nécessaires pour gagner : 1, 2, 2, 3 pour BO1, BO2, BO3, BO5.</summary>
    public int WinsNeeded => Format / 2 + 1;

    public static bool IsValidFormat(int format) => Formats.Contains(format);

    /// <summary>Marque la Série comme modifiée par la commande en cours (voir <see cref="Version"/>).</summary>
    public void Touch() => Version++;

    /// <summary>
    /// Recompte le score à partir des games terminées et en déduit l'issue : gagnée quand une
    /// Équipe atteint le nombre de victoires du format, nulle pour un BO2 à 1-1.
    /// </summary>
    public void ApplyResults(IEnumerable<CustomGame> games)
    {
        var completed = games.Where(g => g.Status == GameStatuses.Completed).ToList();
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

    /// <summary>
    /// Remplace un joueur de la composition courante. Le remplaçant ne reprend pas le rôle de capitaine :
    /// l'organisateur en désigne un nouveau. Un capitaine remplacé perd son accès : le Lien capitaine de
    /// son Équipe est renouvelé. Demande les liens chargés.
    /// </summary>
    public void Remplacer(SeriesPlayer remplace, Player remplacant)
    {
        if (remplace.Capitaine)
            LienCapitaine(remplace.Equipe).Renouveler();

        remplace.Player = remplacant;
        remplace.PlayerId = remplacant.Id;
        remplace.Capitaine = false;
    }

    /// <summary>Fait du joueur le Capitaine de son Équipe, à la place du précédent.</summary>
    public void DesignerCapitaine(SeriesPlayer capitaine)
    {
        foreach (var player in PlayersOf(capitaine.Equipe))
            player.Capitaine = player == capitaine;
    }

    /// <summary>
    /// Une Draft de champions est en cours sur une game de la Série : les rôles de l'Autorole restent
    /// cachés jusqu'à sa fin. Demande les games et leur Draft de champions chargées.
    /// </summary>
    public bool RolesCaches => Games.Any(g => g.DraftChampions is { EstTerminee: false });

    /// <summary>Le rôle attribué par l'Autorole au joueur de la composition courante, null sans Autorole.</summary>
    public Role? RoleOf(int playerId) => Players.FirstOrDefault(p => p.PlayerId == playerId)?.Role;

    /// <summary>Le Capitaine de l'Équipe, ou null tant qu'aucun n'est désigné.</summary>
    public int? CapitaineOf(string equipe) => PlayersOf(equipe).SingleOrDefault(p => p.Capitaine)?.PlayerId;
}

/// <summary>L'issue d'une Série.</summary>
public enum SeriesOutcome
{
    EnCours,
    Gagnee,
    Nulle,
    Interrompue
}
