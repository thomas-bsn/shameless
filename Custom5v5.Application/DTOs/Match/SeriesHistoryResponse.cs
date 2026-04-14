namespace Custom5v5.Application.DTOs.Match;

public record SeriesHistoryResponse(
    int SeriesId,
    int Format,
    DateTime PlayedAt,
    List<GameHistoryResponse> Games
);