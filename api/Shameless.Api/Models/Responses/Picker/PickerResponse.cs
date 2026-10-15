using Shameless.Application.DTOs.Match;
using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.Picker;

/// <summary>
/// La session de Picker de la Soirée ouverte : les dix joueurs cochés, la méthode, la composition
/// courante des deux Équipes (vides tant que rien n'est tiré), leur cost total et leur écart.
/// Pendant une Draft : les capitaines, le premier pick, les picks faits et l'Équipe au trait.
/// </summary>
public sealed record PickerResponse(
    List<GameParticipantDto> Players,
    PickerMethode? Methode,
    RouletteVariante? Variante,
    List<GameParticipantDto> TeamA,
    List<GameParticipantDto> TeamB,
    decimal TeamACost,
    decimal TeamBCost,
    decimal EcartCost,
    bool CanValidate,
    int? CapitaineA,
    int? CapitaineB,
    string? PremierPick,
    List<DraftPickResponse> Picks,
    string? EquipeAuTrait)
{
    public static PickerResponse From(PickerSession session)
    {
        var players = session.Players.OrderBy(p => p.Position).ToList();
        List<GameParticipantDto> Of(string equipe) =>
            players.Where(p => p.Equipe == equipe).Select(p => GameParticipantDto.From(p.Player, p.Role)).ToList();

        var teamA = Of(Equipes.A);
        var teamB = Of(Equipes.B);
        var teamACost = teamA.Sum(p => p.Cost ?? 0);
        var teamBCost = teamB.Sum(p => p.Cost ?? 0);
        return new PickerResponse(
            Players: players.Select(p => GameParticipantDto.From(p.Player)).ToList(),
            Methode: session.Methode,
            Variante: session.Variante,
            TeamA: teamA,
            TeamB: teamB,
            TeamACost: teamACost,
            TeamBCost: teamBCost,
            EcartCost: Math.Abs(teamACost - teamBCost),
            CanValidate: session.CanValidate,
            CapitaineA: session.CapitaineOf(Equipes.A),
            CapitaineB: session.CapitaineOf(Equipes.B),
            PremierPick: session.PremierPick,
            Picks: session.Picks.Select(p => new DraftPickResponse(p.PlayerId, p.Equipe!)).ToList(),
            EquipeAuTrait: session.EquipeAuTrait);
    }
}

/// <summary>Un pick de la Draft : le joueur pris et l'Équipe qui l'a pris.</summary>
public sealed record DraftPickResponse(int PlayerId, string Equipe);
