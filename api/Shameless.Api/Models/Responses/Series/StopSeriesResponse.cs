using Shameless.Application.Services;

namespace Shameless.Api.Models.Responses.Series;

public sealed record StopSeriesResponse(int SeriesId, ArretSerie Arret);
