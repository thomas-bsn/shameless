using Shameless.Application.ChampionDraft;
using Shameless.Application.DTOs.Match;
using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.ChampionDraft;

/// <summary>
/// L'état d'une Draft de champions : les vingt tours de l'ordre Tournament avec l'Équipe attendue à
/// chacun, les tours joués, le tour courant et l'Équipe attendue, les deux
/// Équipes de la game et leurs capitaines. Les rôles de l'Autorole n'apparaissent qu'une fois la Draft
/// terminée. En Fearless, les champions réellement joués dans les games précédentes de la Série
/// (d'après les rapports) : ils restent bannissables et pickables ; null sans Fearless. Aucun lien
/// d'accès n'y figure.
/// </summary>
public sealed record DraftChampionsResponse(
    int GameId,
    int SeriesId,
    bool Fearless,
    string EquipeBlue, // "A" | "B"
    string EquipeRed,
    bool Terminee,
    TourCourantResponse? TourCourant, // null une fois la Draft terminée
    List<TourDraftResponse> Tours,
    List<ActionDraftResponse> Actions,
    List<GameParticipantDto> TeamA,
    List<GameParticipantDto> TeamB,
    int? CapitaineA,
    int? CapitaineB,
    List<string>? ChampionsDejaJoues, // null sans Fearless
    ResetDemandeResponse? ResetDemande) // null sans demande de Reset en attente
{
    public static DraftChampionsResponse From(DraftChampions draft, DateTime maintenant)
    {
        var game = draft.Game;
        var series = game.Series!;
        var tour = draft.TourCourant;
        return new DraftChampionsResponse(
            GameId: game.Id,
            SeriesId: series.Id,
            Fearless: series.Fearless,
            EquipeBlue: draft.EquipeBlue,
            EquipeRed: Equipes.Other(draft.EquipeBlue),
            Terminee: draft.EstTerminee,
            TourCourant: tour == null
                ? null
                : new TourCourantResponse(tour.Numero, tour.Action, tour.Side, draft.EquipeDuTour(tour.Numero), draft.TourCommenceLe, draft.Echeance),
            Tours: OrdreTournament.Tours
                .Select(t => new TourDraftResponse(t.Numero, t.Action, t.Side, draft.EquipeDuTour(t.Numero)))
                .ToList(),
            Actions: draft.Actions.OrderBy(a => a.Numero).Select(a =>
            {
                var joue = OrdreTournament.Tour(a.Numero);
                return new ActionDraftResponse(a.Numero, joue.Action, joue.Side, draft.EquipeDuTour(a.Numero), a.ChampionId);
            }).ToList(),
            TeamA: Joueurs(Equipes.A),
            TeamB: Joueurs(Equipes.B),
            CapitaineA: series.CapitaineOf(Equipes.A),
            CapitaineB: series.CapitaineOf(Equipes.B),
            ChampionsDejaJoues: draft.ChampionsDejaJoues?.ToList(),
            ResetDemande: draft.ResetEnAttente(maintenant)
                ? new ResetDemandeResponse(draft.ResetDemandeParEquipe!, draft.ResetDemandeLe!.Value, draft.ResetExpireLe!.Value)
                : null);

        List<GameParticipantDto> Joueurs(string equipe) =>
            game.ParticipantsOf(equipe)
                .Select(p => GameParticipantDto.From(p.Player, draft.RoleRevele(p.PlayerId)))
                .ToList();
    }
}

/// <summary>Une demande de Reset de la draft en attente : l'Équipe qui la demande, quand, et quand elle expire (60 secondes).</summary>
public sealed record ResetDemandeResponse(string Equipe, DateTime DemandeLe, DateTime ExpireLe);

/// <summary>Le tour à jouer : son numéro (1 à 20), "ban" | "pick", le Side et l'Équipe attendue, quand il a commencé et son
/// échéance (60 secondes plus tard), après laquelle l'organisateur connecté peut jouer à la place de l'Équipe attendue.
/// </summary>
public sealed record TourCourantResponse(int Numero, string Action, string Side, string Equipe, DateTime CommenceLe, DateTime Echeance);

/// <summary>Un tour de l'ordre Tournament pour cette game : numéro (1 à 20), "ban" | "pick", le Side et l'Équipe qui le joue.</summary>
public sealed record TourDraftResponse(int Numero, string Action, string Side, string Equipe);

/// <summary>Un tour joué : le champion banni ou pick pour l'Équipe, sans joueur associé.</summary>
public sealed record ActionDraftResponse(int Numero, string Action, string Side, string Equipe, string ChampionId);
