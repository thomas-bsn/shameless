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
    /// <summary>Le lancement de la Game dans la Série (« Lancer la draft », ADR 0017).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Le début de la Game : le signal « partie lancée » du Companion ou de l'organisateur (ADR 0017).
    /// Null tant que la partie n'est pas lancée ; c'est ce moment qui clôt les pronostics.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>Quand le résultat a été saisi, null tant que la game n'est pas terminée.</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Quand un Administrateur a corrigé (vainqueur, composition) cette game terminée ; null si elle est
    /// telle que saisie. La game garde ainsi la trace que ses données ne sont plus celles d'origine.
    /// </summary>
    public DateTime? ModifieeParAdminLe { get; set; }

    /// <summary>
    /// Vrai quand un Administrateur a corrigé le vainqueur ou la composition. Annuler la dernière correction
    /// de stat ne retire la marque « modifiée par un admin » que si ce n'est pas le cas.
    /// </summary>
    public bool VainqueurOuCompositionCorriges { get; set; }
    public int? SeriesId { get; set; }
    public BestOfSeries? Series { get; set; }

    /// <summary>La Draft de champions de la game, null tant qu'elle n'est pas ouverte.</summary>
    public DraftChampions? DraftChampions { get; set; }


    /// <summary>Les rapports du Companion mis en revue pour cette game, décidés ou non.</summary>
    public ICollection<RapportCompanion> Rapports { get; set; } = new List<RapportCompanion>();

    // Navigation properties
    public ICollection<CustomGameParticipant> Participants { get; set; } = new List<CustomGameParticipant>();

    /// <summary>Le rapport qui attend la décision de l'organisateur, null s'il n'y en a pas.</summary>
    public RapportCompanion? RapportEnRevue => Rapports.SingleOrDefault(r => r.Statut == StatutRapport.EnRevue);

    public string TeamBSide => Sides.Other(TeamASide);

    /// <summary>
    /// Le rang de la game enregistrée dans sa Série, à partir de 1, dans l'ordre de lancement ; 1 pour
    /// une game legacy hors Série. Demande <c>Series.Games</c> chargé.
    /// </summary>
    public int NumeroDansLaSerie => Series == null ? 1 : Series.Games.Count(g => g.Id <= Id);

    /// <summary>L'Équipe qui joue le Side donné dans cette game.</summary>
    public string EquipeOn(string side) => side == TeamASide ? Equipes.A : Equipes.B;

    /// <summary>
    /// Garde sur la game et ses participants ce que dit un rapport du Companion (conforme, ou accepté
    /// après revue) : identifiant et données Riot de la partie, champion joué et stats de chaque
    /// participant détecté. Un participant absent du rapport n'a ni champion ni stats.
    /// </summary>
    public void AppliquerRapport(RapportDeGame rapport)
    {
        // Le Rapport fait foi sur le Side : s'il diffère de celui prévu, la game garde le camp réellement joué.
        if (rapport.InverseLesSidesDe(this))
        {
            TeamASide = Sides.Other(TeamASide);
            foreach (var participant in Participants)
                participant.Side = Sides.Other(participant.Side);
        }

        RiotMatchId = rapport.RiotMatchId;
        RiotMatchData = rapport.Donnees;
        var joueurs = rapport.Joueurs.ToDictionary(j => j.Puuid);
        foreach (var participant in Participants)
        {
            if (!joueurs.TryGetValue(participant.Player.PUUID, out var joueur)) continue;
            participant.ChampionId = joueur.ChampionId;
            participant.RiotParticipantData = joueur.Stats;
        }
    }

    /// <summary>La composition d'une Équipe pour cette game.</summary>
    public IEnumerable<CustomGameParticipant> ParticipantsOf(string equipe) =>
        Participants.Where(p => EquipeOn(p.Side) == equipe);
}
