using Shameless.Application.DTOs.Match;
using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.Picker;

/// <summary>
/// La session de Picker de la Soirée ouverte : les dix joueurs cochés, la méthode, la composition
/// courante des deux Équipes (vides tant que rien n'est tiré), leur cost total et leur écart.
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
    bool CanValidate)
{
    public static PickerResponse From(PickerSession session)
    {
        var players = session.Players.OrderBy(p => p.Position).ToList();
        List<GameParticipantDto> Of(string equipe) =>
            players.Where(p => p.Equipe == equipe).Select(p => GameParticipantDto.From(p.Player)).ToList();

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
            CanValidate: session.CanValidate);
    }
}
