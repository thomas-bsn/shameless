namespace Shameless.Api.Models.Requests.ChampionDraft;

/// <summary>
/// Une action de la Draft de champions : le numéro du tour attendu (1 à 20) et le champion banni ou
/// pick, par son identifiant Data Dragon (ex. "LeeSin").
/// </summary>
public sealed record JouerDraftChampionsRequest(int Tour, string ChampionId);
