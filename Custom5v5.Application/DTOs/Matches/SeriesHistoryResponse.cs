using Custom5v5.Api.Contracts.Matches;

namespace Custom5v5.Application.DTOs.Matches;

public record SeriesHistoryResponse(
    int SeriesId,
    int Format,
    DateTime PlayedAt,
    List<GameHistoryResponse> Games
);