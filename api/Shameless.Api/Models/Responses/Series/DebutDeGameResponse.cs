using Shameless.Application.Entities;

namespace Shameless.Api.Models.Responses.Series;

/// <summary>
/// Le début d'une Game, après le signal « partie lancée » (Companion ou organisateur, ADR 0017) : sa
/// game et le moment du début. Un signal répété répond le même début.
/// </summary>
public sealed record DebutDeGameResponse(int SeriesId, int GameId, DateTime StartedAt)
{
    public static DebutDeGameResponse From(CustomGame game) => new(game.SeriesId!.Value, game.Id, game.StartedAt!.Value);
}
