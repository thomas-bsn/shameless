namespace Shameless.Api.Models.Responses.Series;

public sealed record CreateSeriesResponse(int SeriesId);

public sealed record ActiveSeriesResponse(List<Application.DTOs.Match.SeriesResponse> Series);
