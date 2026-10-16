using Shameless.Application.Companion;

namespace Shameless.Application.Entities;

public class CustomGame
{
    public int Id { get; set; }
    public DateTime PlayedAt { get; set; }
    public string TeamASide { get; set; } = Sides.Blue; // Side joué par l'Équipe A : "blue" | "red"
    public string? WinningTeam { get; set; } // Équipe gagnante : "A" | "B", null tant que la game n'est pas terminée
    /// <summary>L'identifiant Riot stable de la partie, donné par le rapport du Companion ; null sans rapport.</summary>
    public string? RiotMatchId { get; set; }

    /// <summary>Les données Riot brutes de la partie (JSON), données par le rapport du Companion.</summary>
    public string? RiotMatchData { get; set; }
    public string Status { get; set; } = GameStatuses.Pending; // voir GameStatuses
    public DateTime CreatedAt { get; set; }

    /// <summary>Quand le résultat a été saisi, null tant que la game n'est pas terminée.</summary>
    public DateTime? CompletedAt { get; set; }
    public int? SeriesId { get; set; }
    public BestOfSeries? Series { get; set; }

    /// <summary>La Draft de champions de la game, null tant qu'elle n'est pas ouverte.</summary>
    public DraftChampions? DraftChampions { get; set; }


    // Navigation properties
    public ICollection<CustomGameParticipant> Participants { get; set; } = new List<CustomGameParticipant>();

    public string TeamBSide => Sides.Other(TeamASide);

    /// <summary>L'Équipe qui joue le Side donné dans cette game.</summary>
    public string EquipeOn(string side) => side == TeamASide ? Equipes.A : Equipes.B;

    /// <summary>
    /// Garde sur la game et ses participants ce que dit un rapport conforme du Companion : identifiant
    /// et données Riot de la partie, champion joué et stats de chaque joueur.
    /// </summary>
    public void AppliquerRapport(RapportDeGame rapport)
    {
        RiotMatchId = rapport.RiotMatchId;
        RiotMatchData = rapport.Donnees;
        var joueurs = rapport.Joueurs.ToDictionary(j => j.Puuid);
        foreach (var participant in Participants)
        {
            var joueur = joueurs[participant.Player.PUUID];
            participant.ChampionId = joueur.ChampionId;
            participant.RiotParticipantData = joueur.Stats;
        }
    }

    /// <summary>La composition d'une Équipe pour cette game.</summary>
    public IEnumerable<CustomGameParticipant> ParticipantsOf(string equipe) =>
        Participants.Where(p => EquipeOn(p.Side) == equipe);
}
