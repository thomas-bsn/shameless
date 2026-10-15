namespace Shameless.Application.DTOs.Match;

public record SeriesHistoryResponse(
    int SeriesId,
    int Format,
    DateTime PlayedAt,
    List<string>? TeamAReference,
    List<GameHistoryResponse> Games
);