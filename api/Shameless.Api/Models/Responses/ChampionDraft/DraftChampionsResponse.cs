using Shameless.Application.ChampionDraft;
using Shameless.Application.DTOs.Match;
using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.ChampionDraft;

/// <summary>
/// L'état d'une Draft de champions : les tours joués, le tour courant et l'Équipe attendue, les deux
/// Équipes de la game et leurs capitaines. Les rôles de l'Autorole n'apparaissent qu'une fois la Draft
/// terminée. Aucun lien d'accès n'y figure.
/// </summary>
public sealed record DraftChampionsResponse(
    int GameId,
    int SeriesId,
    bool Fearless,
    string EquipeBlue, // "A" | "B"
    string EquipeRed,
    bool Terminee,
    TourCourantResponse? TourCourant, // null une fois la Draft terminée
    List<ActionDraftResponse> Actions,
    List<GameParticipantDto> TeamA,
    List<GameParticipantDto> TeamB,
    int? CapitaineA,
    int? CapitaineB)
{
    public static DraftChampionsResponse From(DraftChampions draft)
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
                : new TourCourantResponse(tour.Numero, tour.Action, tour.Side, draft.EquipeDuTour(tour.Numero), draft.TourCommenceLe),
            Actions: draft.Actions.OrderBy(a => a.Numero).Select(a =>
            {
                var joue = OrdreTournament.Tour(a.Numero);
                return new ActionDraftResponse(a.Numero, joue.Action, joue.Side, draft.EquipeDuTour(a.Numero), a.ChampionId);
            }).ToList(),
            TeamA: Joueurs(Equipes.A),
            TeamB: Joueurs(Equipes.B),
            CapitaineA: series.CapitaineOf(Equipes.A),
            CapitaineB: series.CapitaineOf(Equipes.B));

        List<GameParticipantDto> Joueurs(string equipe) =>
            game.ParticipantsOf(equipe)
                .Select(p => GameParticipantDto.From(p.Player, draft.EstTerminee
                    ? series.Players.FirstOrDefault(s => s.PlayerId == p.PlayerId)?.Role
                    : null))
                .ToList();
    }
}

/// <summary>Le tour à jouer : son numéro (1 à 20), "ban" | "pick", le Side et l'Équipe attendue, et quand il a commencé.</summary>
public sealed record TourCourantResponse(int Numero, string Action, string Side, string Equipe, DateTime CommenceLe);

/// <summary>Un tour joué : le champion banni ou pick pour l'Équipe, sans joueur associé.</summary>
public sealed record ActionDraftResponse(int Numero, string Action, string Side, string Equipe, string ChampionId);
