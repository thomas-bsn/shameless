namespace Shameless.Api.Models.Requests.Player;

public sealed record UpdatePeakRequest(
    string? PeakTier,
    int? PeakDivision,
    string? PeakSeason,
    int PeakLp
);