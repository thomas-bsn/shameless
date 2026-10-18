namespace Shameless.Api.Models.Responses.Player;

/// <summary>Les saisons proposées pour un peak (S24 jusqu'à la saison courante) et la saison courante.</summary>
public sealed record PeakSaisonsResponse(IReadOnlyList<string> Saisons, string Courante);
