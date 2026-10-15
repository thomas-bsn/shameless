using Shameless.Application.Entities;

namespace Shameless.Application.DTOs.Match;

/// <summary>Une Série de l'historique : son issue, l'Équipe gagnante quand elle est gagnée, et ses games terminées.</summary>
public record SeriesHistoryResponse(
    int SeriesId,
    int Format,
    DateTime PlayedAt,
    SeriesOutcome? Outcome, // null pour une game legacy hors Série
    string? WinningTeam, // "A" | "B" quand la Série est gagnée
    List<GameHistoryResponse> Games
);
