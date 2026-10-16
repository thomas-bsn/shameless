using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.Companion;

/// <summary>Le code Companion d'une Série, pour l'organisateur.</summary>
public sealed record CodeCompanionResponse(int SeriesId, string Code)
{
    public static CodeCompanionResponse From(BestOfSeries series) => new(series.Id, series.CodeCompanion);
}

/// <summary>
/// La Série d'un code Companion, pour que le Companion confirme à quoi il est associé : son format,
/// son score et les RiotId de la composition courante de chaque Équipe.
/// </summary>
public sealed record SerieDuCompanionResponse(
    int SeriesId,
    int Format,
    bool Fearless,
    int TeamAWins,
    int TeamBWins,
    IReadOnlyList<string> EquipeA,
    IReadOnlyList<string> EquipeB)
{
    public static SerieDuCompanionResponse From(BestOfSeries series) => new(
        SeriesId: series.Id,
        Format: series.Format,
        Fearless: series.Fearless,
        TeamAWins: series.TeamAWins,
        TeamBWins: series.TeamBWins,
        EquipeA: RiotIds(series, Equipes.A),
        EquipeB: RiotIds(series, Equipes.B));

    private static List<string> RiotIds(BestOfSeries series, string equipe) =>
        series.PlayersOf(equipe).Select(p => p.Player.RiotId).Order().ToList();
}
